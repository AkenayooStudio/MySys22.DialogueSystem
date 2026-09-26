/*|=====================================[MySys22.DialogueSystem]=====================================|*
*| MySys22.DialogueSystem is a source-available dialogue system engine for Unity.                    |
*| For more information on how to use it, please visit:                                              |
*| GitHub: https://github.com/AkenayooStudio/MySys22.DialogueSystem                                  |
*|                                                                                                   |
*|                                                                                                   |
*| MySys22™ is a trademark used by Akenayō to designate components used to develop                   |
*| other systems and applications created by the studio and its community.                           |
*| Use of the MySys22™ name is regulated by the development studio                                   |
*| Akenayō Entertainment & Technology, and any unauthorized misuse will be penalized.                |
*| The system is published under a proprietary license (RLOCL - Ruby Limited Open Code License)      |
*| For more information, view the folder License on root project                                     |
*|                                                                                                   |
*| [INFORMATION]                                                                                     |
*| Version = 0.5 BETA                                                                                |
*| Release Date = 09-27-2026                                                                         |
*| Update Date = n/A                                                                                 |
*|                                                                                                   |
*| Developer:                                                                                        |
*| Vantaggio Is Purple                                                                               |
*|                                                                                                   |
*| Contact:                                                                                          |
*|  Support => support@akenayo.com                                                                   |
*|  Legal => legal@akenayo.com                                                                       |
*| © 2024-2025 Akenayō Ruby | MySys22™ | Akenayō Entertainment & Technology                          |
*|===================================================================================================|*
*/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Unity.Collections;
using MySys22.DialogueEngine.Core.SIMD;

namespace MySys22.DialogueEngine.Core
{
    public class DialogueGraphPlayer
    {

        public const string DefaultChoiceSpeaker = "system";

        public event Action<string, string> OnLine;

        public event Action<List<ChoiceOption>> OnChoices;
        public event Action OnEnd;

        public event Action<string> OnNodeEntered;

        public event Action<bool> OnWaitingForActionChanged;

        private GraphData _graph;
        private GraphData _rootGraph;
        private readonly Stack<GraphReturnPoint> _graphStack = new Stack<GraphReturnPoint>();
        private ILineProvider _lineProvider;
        private NodeData _currentNode;
        private int _currentLineIndex;
        private List<int> _lineIds;
        private List<ChoiceOption> _currentChoices = new List<ChoiceOption>();

        private bool _isRunning;
        private bool _waitingForChoice;
        private bool _waitingForAction;

        private CancellationTokenSource _parallelActionCts;
        private Task _parallelActionTask;

        private QuestVariableMirror _questMirror;
        private DialogueActionContext _pendingActionContext;
        private string _pendingActionId;
        private Task _pendingActionTask;
        private CancellationTokenSource _pendingActionCts;

        private sealed class GraphReturnPoint
        {
            public GraphData Graph;
            public string NodeId;
        }

        public bool EnableSimdOptimizations { get; set; } = true;

        public float FunctionTimeoutSeconds { get; set; }

        public Func<string, GraphData> GraphResolver { get; set; } = DialogueProjectLoader.LoadGraphByName;

        public DialogueVariables Variables { get; set; } = new DialogueVariables();

        public IQuestService Quests { get; set; } = QuestRegistry.Default;

        public event Action<string, string> OnGraphJumped;

        public event Action<DialoguePosition> OnPositionChanged;

        public string FunctionProgress { get; private set; }

        public void SetFunctionProgress(string progress)
        {
            FunctionProgress = progress;
            RaisePosition();
        }

        public bool IsRunning => _isRunning;
        public bool IsWaitingForChoice => _waitingForChoice;

        public bool IsWaitingForAction => _waitingForAction;

        public string PendingActionId => _pendingActionId;

        public GraphData Graph => _graph;
        public NodeData CurrentNode => _currentNode;
        public string CurrentNodeId => _currentNode?.Id;
        public string CurrentSpeaker => _currentNode?.Speaker;

        public int CurrentLineCount => _lineIds?.Count ?? 0;

        public int CurrentLineIndex => _currentLineIndex;

        public IReadOnlyList<ChoiceOption> CurrentChoices =>
            _waitingForChoice ? _currentChoices : (IReadOnlyList<ChoiceOption>)Array.Empty<ChoiceOption>();

        public void Start(GraphData graph, ILineProvider lineProvider)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (lineProvider == null) throw new ArgumentNullException(nameof(lineProvider));

            DialogueVariableCatalog.ApplyTo(Variables);
            RefreshIntegrations();

            _graph = graph;
            _rootGraph = graph;
            _graphStack.Clear();
            _lineProvider = lineProvider;
            _isRunning = true;
            _waitingForChoice = false;
            ClearPendingAction();

            DialogueLogger.Log($"Starting dialogue: {graph.GraphId}");

            string startId = graph.StartNode;
            if (string.IsNullOrEmpty(startId))
            {
                var startNode = graph.Nodes.FirstOrDefault(n => n.IsStart);
                if (startNode != null)
                {
                    startId = startNode.Id;
                    DialogueLogger.Log($"StartNode not set, using IsStart node: {startId}");
                }
                else
                {
                    DialogueLogger.LogError("303", "No start node specified", "Graph missing StartNode or IsStart marker");
                    throw new InvalidOperationException("No start node specified.");
                }
            }

            _currentNode = FindNode(startId);
            if (_currentNode == null)
            {
                DialogueLogger.LogError("303", "Start node not found", startId);
                _isRunning = false;
                return;
            }

            if (EnableSimdOptimizations && SimdHelper.UseSimd)
                DialogueLogger.Log($"SIMD active: {SimdHelper.GetBestArchitecture()}");

            EnterNode(_currentNode);
        }

        public void Restart()
        {
            GraphData root = _rootGraph ?? _graph;
            if (root == null) return;
            Stop();
            Start(root, _lineProvider);
        }

        public void RefreshIntegrations()
        {
            _questMirror?.Dispose();
            _questMirror = QuestVariableMirror.Attach(Quests, Variables);

            if (_questMirror != null)
                DialogueLogger.Log("Quest -> variable mirroring active.");
        }

        public void DisposeIntegrations()
        {
            _questMirror?.Dispose();
            _questMirror = null;
        }

        public void SetLineProvider(ILineProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            _lineProvider = provider;
        }

        public void Continue()
        {
            if (!_isRunning) return;

            if (_waitingForAction)
            {
                DialogueLogger.LogWarning(
                    $"Continue() ignored: waiting for action '{_pendingActionId}' to finish.");
                return;
            }

            if (_waitingForChoice)
            {
                DialogueLogger.LogWarning("Continue() called while waiting for choice. Use Choose().");
                return;
            }

            if (_currentNode != null && _currentNode.Type == "choice")
            {
                DialogueLogger.LogError("312", "Invalid state", "Choice node but _waitingForChoice is false");
                OnEnd?.Invoke();
                _isRunning = false;
                return;
            }

            if (_currentNode != null && _currentNode.Type == "dialogue")
            {
                _currentLineIndex++;
                if (_currentLineIndex < _lineIds.Count)
                {
                    int did = _lineIds[_currentLineIndex];
                    string text = _lineProvider.GetLine(_currentNode.Speaker, did);
                    PlayVoice(_currentNode, did);
                    OnLine?.Invoke(_currentNode.Speaker, text);
                    RaisePosition();
                    return;
                }
                else
                {
                    ExecuteOnComplete(_currentNode);
                    ExitNode(_currentNode);
                    return;
                }
            }

            DialogueLogger.LogWarning("Continue() called in invalid state.");
        }

        public void Choose(int index)
        {
            if (!_isRunning) return;
            if (!_waitingForChoice)
            {
                DialogueLogger.LogWarning("Choose() called but not waiting for a choice.");
                return;
            }

            if (_currentNode == null || _currentNode.Type != "choice")
            {
                DialogueLogger.LogWarning("Choose() called but current node is not a choice.");
                return;
            }

            if (index < 0 || index >= _currentChoices.Count)
            {
                DialogueLogger.LogError("310", "Choice index out of range",
                    $"Index {index}, count {_currentChoices.Count}");
                return;
            }

            ChoiceOption selected = _currentChoices[index];
            DialogueLogger.Log($"Choice selected: '{selected.Text}' -> next: {selected.Next}");

            if (!string.IsNullOrEmpty(selected.OnChosen))
            {
                IAction action = ActionRegistry.Resolve(selected.OnChosen);
                if (action != null)
                {
                    var context = BuildContext(_currentNode, selected.OnChosen, null);
                    try { action.Execute(context); }
                    catch (Exception ex)
                    {
                        DialogueLogger.LogError("305", "Choice action error",
                            $"{selected.OnChosen}: {ex.Message}");
                    }
                }
            }

            _waitingForChoice = false;
            _currentChoices.Clear();

            if (!string.IsNullOrEmpty(selected.Next))
            {
                var nextNode = FindNode(selected.Next);
                if (nextNode != null)
                {
                    _currentNode = nextNode;
                    EnterNode(_currentNode);
                    return;
                }
                else
                    DialogueLogger.LogError("303", "Target node not found", selected.Next);
            }

            EndOrReturn();
        }

        public void Stop()
        {
            _isRunning = false;
            _waitingForChoice = false;
            _currentChoices.Clear();
            CancelParallelAction();
            ClearPendingAction();
            DialogueLogger.Log("Dialogue stopped.");
        }

        public bool Tick()
        {
            if (!_waitingForAction || _pendingActionTask == null) return false;

            if (!_pendingActionTask.IsCompleted) return true;

            Task task = _pendingActionTask;
            string actionId = _pendingActionId;

            _pendingActionTask = null;
            _pendingActionId = null;
            _pendingActionCts?.Dispose();
            _pendingActionCts = null;
            _waitingForAction = false;
            OnWaitingForActionChanged?.Invoke(false);

            if (task.IsFaulted)
            {
                Exception error = task.Exception?.GetBaseException();
                DialogueLogger.LogError("305", "Action failed", $"{actionId}: {error?.Message}");
            }
            else if (task.IsCanceled)
            {
                DialogueLogger.Log($"Action '{actionId}' was cancelled; dialogue will not continue.");
                return false;
            }
            else
            {
                DialogueLogger.Log($"Action '{actionId}' completed. Resuming dialogue.");
            }

            if (!_isRunning) return false;

            NodeData node = _currentNode;
            DialogueFunctionResult result = _pendingActionContext?.Result ?? DialogueFunctionResult.Neutral;
            _pendingActionContext = null;
            DialogueActionContext.SetCurrent(null);

            if (node != null) ExecuteOnComplete(node);

            if (node != null && node.Type == "function")
            {
                AdvanceFunctionNode(node, result);
                return false;
            }

            ExitNode(node);
            return false;
        }

        public List<int> FindNodesByTypeAndSpeaker(string type, string speaker = null)
        {
            if (_graph == null || _graph.Nodes.Count == 0)
                return new List<int>();

            if (EnableSimdOptimizations && SimdHelper.UseSimd)
            {
                try
                {
                    var indices = SimdOptimizer.FilterNodesSimd(_graph, type, speaker, null, -1f, Allocator.Temp);
                    var result = new List<int>();
                    for (int i = 0; i < indices.Length; i++) result.Add(indices[i]);
                    indices.Dispose();
                    return result;
                }
                catch (Exception ex)
                {
                    DialogueLogger.LogError("314", "SIMD filter failed, falling back to scalar", ex.Message);
                    return FindNodesByTypeAndSpeakerScalar(type, speaker);
                }
            }
            return FindNodesByTypeAndSpeakerScalar(type, speaker);
        }

        private List<int> FindNodesByTypeAndSpeakerScalar(string type, string speaker)
        {
            var result = new List<int>();
            for (int i = 0; i < _graph.Nodes.Count; i++)
            {
                var node = _graph.Nodes[i];
                if (node.Type == type && (speaker == null || node.Speaker == speaker))
                    result.Add(i);
            }
            return result;
        }

        private NodeData FindNode(string nodeId) => _graph.Nodes.Find(n => n.Id == nodeId);

        private void EnterNode(NodeData node)
        {
            _currentNode = node;
            FunctionProgress = null;

            if (IsNodeSkipped(node))
            {

                ExitNode(node);
                return;
            }

            OnNodeEntered?.Invoke(node?.Id);
            PlayNodeAudio(node);
            RaisePosition();
            switch (node.Type)
            {
                case "dialogue": EnterDialogueNode(node); break;
                case "choice":   EnterChoiceNode(node);   break;
                case "function": EnterFunctionNode(node); break;
                case "condition": EnterConditionNode(node); break;
                case "jump":     EnterJumpNode(node);     break;
                case "end":      EnterEndNode(node);      break;
                default:
                    DialogueLogger.LogError("304", "Unknown node type", node.Type);
                    OnEnd?.Invoke(); _isRunning = false; break;
            }
        }

        public bool EvaluateCondition(string expression, out string error)
            => DialogueExpression.Evaluate(expression, Variables, out _, out error);

        public bool EvaluateCondition(string expression) => EvaluateCondition(expression, out _);

        private void EnterConditionNode(NodeData node)
        {
            if (string.IsNullOrWhiteSpace(node.Condition))
            {
                DialogueLogger.LogError("306", "Condition node has no condition", node.Id);
                EndOrReturn();
                return;
            }

            bool result = EvaluateCondition(node.Condition, out string error);
            if (!string.IsNullOrEmpty(error))
                DialogueLogger.LogWarning($"Condition '{node.Condition}' on node {node.Id} failed: {error}");

            string targetId = result ? node.Next : node.Else;
            DialogueLogger.Log($"Condition {node.Condition} -> {result} (go to {targetId ?? "end"})");

            if (string.IsNullOrWhiteSpace(targetId))
            {
                EndOrReturn();
                return;
            }

            NodeData target = FindNode(targetId);
            if (target == null)
            {
                DialogueLogger.LogError("303", "Condition branch target not found", $"{node.Id} -> {targetId}");
                EndOrReturn();
                return;
            }

            _currentNode = target;
            EnterNode(_currentNode);
        }

        private bool IsNodeSkipped(NodeData node)
        {
            if (string.IsNullOrWhiteSpace(node.Condition)) return false;
            if (node.Type != "dialogue" && node.Type != "function") return false;

            bool result = EvaluateCondition(node.Condition, out string error);
            if (!string.IsNullOrEmpty(error))
                DialogueLogger.LogWarning($"Condition '{node.Condition}' on node {node.Id} failed: {error}");

            if (!result) DialogueLogger.Log($"Node {node.Id} skipped: condition '{node.Condition}' is false.");
            return !result;
        }

        private void EnterDialogueNode(NodeData node)
        {
            if (node.StartDid == null || node.EndDid == null)
            {
                DialogueLogger.LogError("306", "Missing DID range", $"Node {node.Id} has null start/end DID");
                OnEnd?.Invoke(); _isRunning = false; return;
            }
            if (string.IsNullOrEmpty(node.Speaker))
            {
                DialogueLogger.LogError("306", "Missing speaker", $"Node {node.Id} has no speaker");
                OnEnd?.Invoke(); _isRunning = false; return;
            }

            StartParallelAction(node.OnParallel);

            _lineIds = new List<int>();
            for (int did = node.StartDid.Value; did <= node.EndDid.Value; did++)
                _lineIds.Add(did);

            if (_lineIds.Count == 0)
            {
                DialogueLogger.LogWarning($"Dialogue node {node.Id} has empty DID range.");
                ExecuteOnComplete(node);
                ExitNode(node);
                return;
            }

            _currentLineIndex = 0;
            int firstDid = _lineIds[0];
            string text = _lineProvider.GetLine(node.Speaker, firstDid);
            PlayVoice(node, firstDid);
            OnLine?.Invoke(node.Speaker, text);
            RaisePosition();
        }

        private void PlayNodeAudio(NodeData node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Audio)) return;
            DialogueAudio.Play(node.Audio, node.AudioLoop);
        }

        private void PlayVoice(NodeData node, int did)
        {
            if (node == null || !node.Voice) return;
            DialogueAudio.PlayVoice(node.Speaker, did);
        }

        private void StopNodeAudio()
        {
            if (_currentNode != null && _currentNode.AudioLoop) return;
            DialogueAudio.Stop();
        }

        public DialoguePosition GetPosition()
        {
            return new DialoguePosition
            {
                GraphId = _graph?.GraphId,
                NodeId = _currentNode?.Id,
                NodeType = _currentNode?.Type,
                Speaker = _currentNode?.Speaker,
                LineIndex = _currentLineIndex,
                Did = _lineIds != null && _currentLineIndex >= 0 && _currentLineIndex < _lineIds.Count
                    ? _lineIds[_currentLineIndex]
                    : 0,
                LineCount = _lineIds?.Count ?? 0,
                WaitingForChoice = _waitingForChoice,
                WaitingForAction = _waitingForAction,
                FunctionActionId = _waitingForAction ? _pendingActionId : null,
                FunctionProgress = FunctionProgress,
                Language = LanguageManager.CurrentLanguage,
                Running = _isRunning
            };
        }

        private void RaisePosition() => OnPositionChanged?.Invoke(GetPosition());

        private void EnterChoiceNode(NodeData node)
        {
            if (node.Choices == null || node.Choices.Count == 0)
            {
                DialogueLogger.LogError("306", "Choice node has no choices", node.Id);
                OnEnd?.Invoke(); _isRunning = false; return;
            }

            string speaker = string.IsNullOrWhiteSpace(node.Speaker) ? DefaultChoiceSpeaker : node.Speaker;

            _currentChoices = new List<ChoiceOption>(node.Choices.Count);
            for (int i = 0; i < node.Choices.Count; i++)
            {
                ChoiceData choice = node.Choices[i];

                if (!string.IsNullOrWhiteSpace(choice.Condition) && !EvaluateCondition(choice.Condition, out string condError))
                {
                    DialogueLogger.Log($"Option '{choice.Text}' hidden: '{choice.Condition}' is false.");
                    if (!string.IsNullOrEmpty(condError))
                        DialogueLogger.LogWarning($"Option condition failed: {condError}");
                    continue;
                }

                string text = choice.Text;

                if (choice.TextDid > 0)
                {
                    if (_lineProvider is YamlLineProvider yaml &&
                        yaml.TryGetLine(speaker, choice.TextDid, out string localized) &&
                        !string.IsNullOrEmpty(localized))
                    {
                        text = localized;
                    }
                    else
                    {
                        string resolved = _lineProvider.GetLine(speaker, choice.TextDid);
                        if (!string.IsNullOrEmpty(resolved) && !resolved.StartsWith("[MISSING"))
                            text = resolved;
                        else
                            DialogueLogger.LogWarning(
                                $"Choice text_did {choice.TextDid} (speaker '{speaker}') not found in " +
                                $"'{LanguageManager.CurrentLanguage}'; using the inline label.");
                    }
                }

                _currentChoices.Add(new ChoiceOption
                {
                    Index = _currentChoices.Count,
                    Text = text,
                    Next = choice.Next,
                    OnChosen = choice.OnChosen,
                    TextDid = choice.TextDid,
                    Speaker = speaker,
                    Source = choice
                });
            }

            if (_currentChoices.Count == 0)
            {
                DialogueLogger.LogError("306", "No selectable option", $"Node {node.Id}: every option is gated off.");
                EndOrReturn();
                return;
            }

            _waitingForChoice = true;
            OnChoices?.Invoke(_currentChoices);
            RaisePosition();
        }

        private void EnterFunctionNode(NodeData node)
        {
            if (string.IsNullOrEmpty(node.Action))
            {
                DialogueLogger.LogError("306", "Function node has no action", node.Id);
                OnEnd?.Invoke(); _isRunning = false; return;
            }

            DialogueLogger.Log($"Executing function: {node.Action} (wait: {node.Wait})");

            IAction action = ActionRegistry.Resolve(node.Action);
            if (action == null)
            {

                AdvanceFunctionNode(node);
                return;
            }

            DialogueActionContext context = BuildContext(node, node.Action, node.Params);
            context.Completion = new DialogueActionCompletion();
            DialogueActionContext.SetCurrent(context);

            if (!node.Wait)
            {
                try { action.Execute(context); }
                catch (Exception ex)
                {
                    DialogueLogger.LogError("305", "Action execution error", $"{node.Action}: {ex.Message}");
                }
                DialogueActionContext.SetCurrent(null);
                AdvanceFunctionNode(node, context.Result);
                return;
            }

            BeginPendingAction(node, action, context);
        }

        private void BeginPendingAction(NodeData node, IAction action, DialogueActionContext context)
        {
            ClearPendingAction();

            _pendingActionCts = new CancellationTokenSource();
            _pendingActionId = node.Action;
            _waitingForAction = true;
            _pendingActionContext = context;

            _pendingActionTask = ActionRegistry.CompletesExternally(node.Action)
                ? WaitForExternalCompletionAsync(action, context, _pendingActionCts.Token)
                : ExecuteOnCallerContextAsync(action, context, _pendingActionCts.Token);

            DialogueLogger.Log($"Waiting for action '{node.Action}' before node '{node.Next ?? "end"}'.");
            OnWaitingForActionChanged?.Invoke(true);
            RaisePosition();
        }

        private async Task WaitForExternalCompletionAsync(IAction action, DialogueActionContext context,
            CancellationToken token)
        {
            try
            {
                Task started = action.ExecuteAsync(context, token);
                _ = started;

                if (FunctionTimeoutSeconds > 0f)
                {
                    Task delay = Task.Delay(TimeSpan.FromSeconds(FunctionTimeoutSeconds), token);
                    Task finished = await Task.WhenAny(context.Completion.Task, delay).ConfigureAwait(false);
                    if (finished != context.Completion.Task)
                    {
                        DialogueLogger.LogWarning(
                            $"Function '{context.ActionId}' did not complete within " +
                            $"{FunctionTimeoutSeconds:0.##}s; releasing the node.");
                        return;
                    }
                }
                else
                {
                    await context.Completion.Task.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        private void AdvanceFunctionNode(NodeData node, DialogueFunctionResult result = DialogueFunctionResult.Neutral)
        {
            string target = ResolveFunctionTarget(node, result);

            if (!string.IsNullOrEmpty(target))
            {
                NodeData nextNode = FindNode(target);
                if (nextNode != null) { _currentNode = nextNode; EnterNode(_currentNode); return; }
                DialogueLogger.LogError("303", "Target node not found", $"{node.Id} -> {target}");
            }

            EndOrReturn();
        }

        private string ResolveFunctionTarget(NodeData node, DialogueFunctionResult result)
        {
            if (result == DialogueFunctionResult.Neutral &&
                node.Conditions != null && node.Conditions.Count > 0)
            {
                DialogueLogger.LogWarning(
                    $"Function {node.Id} closed without a result (neutral): its condition nodes are ignored. " +
                    "Call Dialogue.Complete(true/false) or ctx.SetResult(...) before it returns.");
            }

            if (result == DialogueFunctionResult.Neutral || node.Conditions == null) return node.Next;

            foreach (string conditionId in node.Conditions)
            {
                if (string.IsNullOrWhiteSpace(conditionId)) continue;

                NodeData condition = FindNode(conditionId);
                if (condition == null || condition.Type != "condition")
                {
                    DialogueLogger.LogWarning($"Function {node.Id}: condition node '{conditionId}' not found.");
                    continue;
                }

                string target = result == DialogueFunctionResult.True ? condition.OnTrue : condition.OnFalse;

                if (string.IsNullOrWhiteSpace(target) && !string.IsNullOrWhiteSpace(condition.Option))
                {
                    string option = condition.Option.Trim().ToLowerInvariant();
                    if (option == (result == DialogueFunctionResult.True ? "true" : "false"))
                        target = condition.Next;
                }

                if (string.IsNullOrWhiteSpace(target)) continue;

                DialogueLogger.Log(
                    $"Function {node.Id} closed with {result}; condition {condition.Id} -> {target}.");
                return target;
            }

            return node.Next;
        }

        private static async Task ExecuteOnCallerContextAsync(IAction action,
            DialogueActionContext context, CancellationToken token)
        {
            await action.ExecuteAsync(context, token).ConfigureAwait(false);
        }

        private static async Task RunParallelAsync(IAction action, DialogueActionContext context,
            CancellationToken token, string actionId)
        {
            try
            {
                await action.ExecuteAsync(context, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                DialogueLogger.Log($"Parallel action '{actionId}' cancelled.");
            }
            catch (Exception ex)
            {
                DialogueLogger.LogError("305", "Parallel action error", $"{actionId}: {ex.Message}");
            }
        }

        private void ClearPendingAction()
        {
            DialogueActionContext.SetCurrent(null);
            _pendingActionContext = null;
            _waitingForAction = false;
            _pendingActionId = null;
            _pendingActionTask = null;
            if (_pendingActionCts != null)
            {
                try { _pendingActionCts.Cancel(); } catch (Exception) {  }
                _pendingActionCts.Dispose();
                _pendingActionCts = null;
            }
        }

        private DialogueActionContext BuildContext(NodeData node, string actionId,
            IReadOnlyDictionary<string, string> parameters)
        {
            return new DialogueActionContext
            {
                ActionId = actionId,
                Node = node,
                Graph = _graph,
                Speaker = node?.Speaker,
                Language = LanguageManager.CurrentLanguage,
                Parameters = parameters ?? node?.Params,
                Player = this,
                Variables = Variables,
                Quests = Quests
            };
        }

        private void EnterJumpNode(NodeData node)
        {
            string targetGraph = string.IsNullOrWhiteSpace(node.Graph) ? null : node.Graph.Trim();
            string targetNode = string.IsNullOrWhiteSpace(node.Next) ? null : node.Next.Trim();

            if (targetGraph == null)
            {
                if (targetNode == null)
                {
                    DialogueLogger.LogError("306", "Jump node has no target", node.Id);
                    EndOrReturn();
                    return;
                }

                NodeData local = FindNode(targetNode);
                if (local == null)
                {
                    DialogueLogger.LogError("303", "Jump target node not found", $"{node.Id} -> {targetNode}");
                    EndOrReturn();
                    return;
                }

                DialogueLogger.Log($"Jump: {node.Id} -> node {targetNode} (same graph)");
                _currentNode = local;
                EnterNode(_currentNode);
                return;
            }

            GraphData target = ResolveGraph(targetGraph);
            if (target == null)
            {
                DialogueLogger.LogError("319", "Jump target graph not found", $"{node.Id} -> {targetGraph}");
                EndOrReturn();
                return;
            }

            string fromId = _graph?.GraphId;

            bool returnsHere = node.Return && !string.IsNullOrWhiteSpace(node.Next);
            string entryId;

            if (returnsHere)
            {
                _graphStack.Push(new GraphReturnPoint { Graph = _graph, NodeId = node.Next.Trim() });
                entryId = ResolveStartNode(target);
                DialogueLogger.Log($"Jump: {fromId} -> {target.GraphId} (returns to {node.Next})");
            }
            else
            {
                entryId = string.IsNullOrWhiteSpace(node.Next) ? ResolveStartNode(target) : node.Next.Trim();
                DialogueLogger.Log($"Jump: {fromId} -> {target.GraphId} (no return)");
            }

            _graph = target;
            OnGraphJumped?.Invoke(fromId, target.GraphId);

            if (string.IsNullOrWhiteSpace(entryId))
            {
                DialogueLogger.LogError("303", "Jump target has no start node", target.GraphId);
                EndOrReturn();
                return;
            }

            NodeData entry = FindNode(entryId);
            if (entry == null)
            {
                DialogueLogger.LogError("303", "Jump target node not found", $"{target.GraphId}#{entryId}");
                EndOrReturn();
                return;
            }

            _currentNode = entry;
            EnterNode(_currentNode);
        }

        private GraphData ResolveGraph(string nameOrId)
        {
            if (GraphResolver == null)
            {
                DialogueLogger.LogWarning("No GraphResolver assigned: cannot follow jump nodes.");
                return null;
            }
            return GraphResolver(nameOrId);
        }

        private static string ResolveStartNode(GraphData graph)
        {
            if (!string.IsNullOrWhiteSpace(graph.StartNode)) return graph.StartNode;
            foreach (NodeData candidate in graph.Nodes)
                if (candidate.IsStart) return candidate.Id;
            return graph.Nodes.Count > 0 ? graph.Nodes[0].Id : null;
        }

        private bool TryReturnFromSubGraph()
        {
            if (_graphStack.Count == 0) return false;

            GraphReturnPoint point = _graphStack.Pop();
            _graph = point.Graph;

            NodeData resume = FindNode(point.NodeId);
            DialogueLogger.Log($"Returning to graph {_graph?.GraphId} at node {point.NodeId}");

            if (resume == null)
            {
                DialogueLogger.LogError("303", "Return node not found", point.NodeId);
                return false;
            }

            _currentNode = resume;
            EnterNode(_currentNode);
            return true;
        }

        private void EndOrReturn()
        {
            if (TryReturnFromSubGraph()) return;
            OnEnd?.Invoke();
            _isRunning = false;
        }

        private void EnterEndNode(NodeData node)
        {
            DialogueLogger.Log($"Reached end node: {node.Id}");
            EndOrReturn();
        }

        private void ExitNode(NodeData node)
        {
            CancelParallelAction();
            StopNodeAudio();

            if (string.IsNullOrEmpty(node.Next))
            {
                DialogueLogger.Log($"Node {node.Id} has no next. Ending dialogue.");
                EndOrReturn(); return;
            }

            var nextNode = FindNode(node.Next);
            if (nextNode == null)
            {
                DialogueLogger.LogError("303", "Target node not found", $"{node.Id} -> {node.Next}");
                OnEnd?.Invoke(); _isRunning = false; return;
            }

            _currentNode = nextNode;
            EnterNode(_currentNode);
        }

        private void ExecuteOnComplete(NodeData node)
        {
            if (string.IsNullOrEmpty(node.OnComplete)) return;
            DialogueLogger.Log($"Executing on_complete: {node.OnComplete}");
            IAction action = ActionRegistry.Resolve(node.OnComplete);
            if (action != null)
            {
                try { action.Execute(BuildContext(node, node.OnComplete, node.Params)); }
                catch (Exception ex)
                {
                    DialogueLogger.LogError("305", "on_complete error", $"{node.OnComplete}: {ex.Message}");
                }
            }
        }

        private void StartParallelAction(string actionId)
        {
            CancelParallelAction();
            if (string.IsNullOrEmpty(actionId)) return;

            IAction action = ActionRegistry.Resolve(actionId);
            if (action == null) return;

            DialogueActionContext context = BuildContext(_currentNode, actionId, _currentNode?.Params);
            _parallelActionCts = new CancellationTokenSource();
            var token = _parallelActionCts.Token;

            _parallelActionTask = RunParallelAsync(action, context, token, actionId);
            DialogueLogger.Log($"Parallel action started: {actionId}");
        }

        private void CancelParallelAction()
        {
            if (_parallelActionCts != null)
            {
                _parallelActionCts.Cancel();
                _parallelActionCts.Dispose();
                _parallelActionCts = null;
            }
            if (_parallelActionTask != null && !_parallelActionTask.IsCompleted)
            {
                _parallelActionTask.ContinueWith(_ => { });
                _parallelActionTask = null;
            }
        }
    }
}

/*
        ~P&@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@&G!.
      J&@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@G:
    ~&@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@Y
   7@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@G
   GBBBBBBBBBB&@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@BBBBBBBBBBBB&@@@@@@@@@@@@@@@@@@@@@@@@@&BBBBBBBBBBB
   :::........Y@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@&............?@@@@@@@@@@@@@@@@@@@@@@@@@?:::::::::::
   :....::....J@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@&:.:::::::...J@@@@@@@@@@@@@@@@@@@@@@@@@?:::::::::::
   :...:::::..J@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@&:.:::::::...J@@@@@@@@@@@@@@@@@@@@@@@@@7:::::::::::
   :..::::::..J@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@&:..::::::...J@@@@@@@@@@@@@@@@@@@@@@@@@7:::::::::::
   ...........J@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@&............?@@@@@@@@@@@@@@@@@@@@@@@@@7...........
   YYYYYYYYYJJB@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@JJJJJJJJJJJ?G@@@@@@@@@@@@@@@@@@@@@@@@@GYYYYYYYYYYY
   @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
   @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
   @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
   @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
   &&&&&&&&&&&&&&&&&&&&######################&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&




   BBBBB#5:::::::::::::::::::::::::::::::::::::::::::::::::::::::::...............:::::::::::::::::::::
   BBBBB#5:::::::::::::::::::::::::::::::::::::::::::::::::::::::::...............:::::::::::::::::::::
   BBBBB#5:::::::::::::::::::::::::::::::::::::::::::::::::::::::::...............:::::::::::::::::::::
   BBBBB#5:::::::::::::::::::::::::::::::::::::::::::::::::::::::::...............:::::::::::::::::::::
   BBBBB#5:::::::::::::::::::::::::::::::::::::::::::::::::::::::::...........^~~^:::::::::::::::::::::                        
   BBBBB#5::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::!JJ?????????7::::::::::::
   BBBBB#5::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::!?JJJJJJJJJJ?^:::::::::::
   BBBBB#5::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::!?JJJJJJJJJJ?^:::::::::::
   BBBBB#5::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::!?JJJJJJJJJJ?^:::::::::::
   B#BBB#5::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::!???JJJJJJJ??^:::::::::::
   ######5::::..............::::::::::::::::::::::::.::..........:::::::::::::Y###########B~.::::::::::
   PPPPPP?::::^!!!!!!!!!!!!^::::::::::::~~~~~~~~~~~~~~~~~~~~~~~~~^::::::::::::P@@@@@@@@@@@@!.::::::::::
   :::::::::::J&@@@@@@@@@@@J::::::::::.^#&&&&&&&&&&&&&&&&&&&&&&&@P.:::::::::::P@@@@@@@@@@@&!.::::::::::
   ::.::.:::::Y&@@@@@@@@@@@Y::::::::::.:#&&&&&&&&&&&&&&&&&&&&&&&@P.:::::::::::P@@@@@@@@@@@&7.::::::::::
   :::::::::::Y&@@@@@@@@@@@Y::::::::::.:B&&&&&&&&&&&&&&&&&&&&&&&@P.:::::::::::P@@@@@@@@@@@&7:::::::::::
   :::::::::::J&@@@@@@@@@@@J:::........:B&&&&&&&&&&&&&&&&&&&&&&&@P........::::P@@@@@@@@@@@&7:..........
   JJJJJJJJJJJP#&&&&&&&&&&&BPPPPPPPPPPPP&&&&&&&&&&&&&&&&&&&&&&&&&#PPPPPPPPPPPPB&&&&&&&&&&&#5JJJJJJJJJJJ
   GBGGGGGGGGGB############&&@@@@@@@@@@@&&&&&&&&&&&&&&&&&&&&&&&&&&@@@@@@@@@@@@&############BGGGGGGGGGGB
   ~PBGGGGGGGGB############&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&############BGGGGGGGGGB?
    :5BBGGGGGGB############&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&############BGGGGGGGBP!
      ~5GBGGGGB############&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&############BGGGGGGP?.
        :7YPGGB############&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&############BGGGPJ^
    Sr.Dev: Vantaggio Is Purple


|≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈|

    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@#Y??????B@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@B?.       J@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@G!          J@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@&?77~           .5@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@B             :J#@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@&5^           :Y&@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@&Y:            J@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@BGGGGGGGGGGGGGBBJ:              G@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@&:                          ~PGGG&@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@&.                        !G@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@&.                      7B@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@#PPGGGG#@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@&.                     ^@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@B7.      ~@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@&.                     ^@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@G!         ~@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@&.                     ^@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@GPP5~           !@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@&.                     ^@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@^             :Y&@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@&.                     .Y&@@@@@@@@@@@@@@@@@@@@@@@@@@&Y.           ^5&@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@&.                       :J#@@@@@@@@@@@@@@@@@@@@@@#J:           ^P@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@&.                         .?#@&&&&&&&&&&&&&&&&@#?.             J@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@Y???????????????!           .::::::::::::::::::.           ~???B@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@B7.                                    .7B@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@#?.                                .?#@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@G                                G@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@#.                               B@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@B                                B@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@B                                B@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@B                                B@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@B                                B@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@B                                B@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@B                                B@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@B                                B@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@B                                B@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@B                                B@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@
    @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@B________________________________B@@@@@@@@@@@@@@@@ ©Akenayō™
                                                                                        
*/