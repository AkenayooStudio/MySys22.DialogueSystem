using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine.Scripting;

namespace MySys22.DialogueEngine.Core
{

    public static class BuiltInActions
    {
        public const string LogDebug = "LogDebug";
        public const string Wait = "Wait";
        public const string QuestAccept = "QuestAccept";
        public const string QuestComplete = "QuestComplete";
        public const string QuestFail = "QuestFail";
        public const string QuestStep = "QuestStep";
        public const string ReportResult = "ReportResult";
        public const string ExternalResult = "ExternalResult";

        public const string ParamQuestId = "questId";
        public const string ParamMessage = "message";
        public const string ParamSeconds = "seconds";
        public const string ParamStepId = "stepId";
        public const string ParamDone = "done";
        public const string ParamResult = "result";
        public const string ParamDelay = "delay";

        public static readonly string[] All =
        {
            LogDebug,
            Wait,
            QuestAccept,
            QuestComplete,
            QuestFail,
            QuestStep,
            ReportResult,
            ExternalResult
        };

        internal static IAction Create(string actionId) => actionId switch
        {
            LogDebug => new LogDebugAction(),
            Wait => new WaitAction(),
            QuestAccept => new QuestAcceptAction(),
            QuestComplete => new QuestCompleteAction(),
            QuestFail => new QuestFailAction(),
            QuestStep => new QuestStepAction(),
            ReportResult => new ReportResultAction(),
            ExternalResult => new ExternalResultAction(),
            _ => null
        };

        internal static ActionRegistry.ActionInfo Info(string actionId) => actionId switch
        {
            LogDebug => new ActionRegistry.ActionInfo
            {
                Id = LogDebug,
                Description = "Writes a line to the Unity console.",
                OptionalParameters = new[] { ParamMessage }
            },
            Wait => new ActionRegistry.ActionInfo
            {
                Id = Wait,
                Description = "Waits N seconds before the dialogue resumes (function node, wait: true).",
                RequiredParameters = new[] { ParamSeconds },
                WaitsForCompletion = true
            },
            QuestAccept => new ActionRegistry.ActionInfo
            {
                Id = QuestAccept,
                Description = "Accepts a quest (state Active).",
                RequiredParameters = new[] { ParamQuestId }
            },
            QuestComplete => new ActionRegistry.ActionInfo
            {
                Id = QuestComplete,
                Description = "Completes a quest (state Completed).",
                RequiredParameters = new[] { ParamQuestId }
            },
            QuestFail => new ActionRegistry.ActionInfo
            {
                Id = QuestFail,
                Description = "Fails a quest (state Failed).",
                RequiredParameters = new[] { ParamQuestId }
            },
            ReportResult => new ActionRegistry.ActionInfo
            {
                Id = ReportResult,
                Description = "Reports True/False to the graph (drives condition nodes). Pairs with wait: true.",
                RequiredParameters = new[] { ParamResult },
                WaitsForCompletion = true
            },
            ExternalResult => new ActionRegistry.ActionInfo
            {
                Id = ExternalResult,
                Description = "Closes itself after a delay through Dialogue.Complete(): template for " +
                              "event driven functions.",
                RequiredParameters = new[] { ParamResult },
                OptionalParameters = new[] { ParamDelay },
                WaitsForCompletion = true,
                CompletesExternally = true
            },
            QuestStep => new ActionRegistry.ActionInfo
            {
                Id = QuestStep,
                Description = "Marks a quest objective done (or pending with done: false).",
                RequiredParameters = new[] { ParamQuestId, ParamStepId },
                OptionalParameters = new[] { ParamDone }
            },
            _ => null
        };

        [DialogueAction(LogDebug, Description = "Writes a line to the Unity console.")]
        public sealed class LogDebugAction : IAction
        {
            public void Execute(DialogueActionContext context)
                => DialogueLogger.Log(context.GetParameter(ParamMessage, "Debug log"));

            public Task ExecuteAsync(DialogueActionContext context, CancellationToken ct)
            {
                Execute(context);
                return Task.CompletedTask;
            }

            public void Cancel() { }
        }

        [DialogueAction(ReportResult, Description = "Reports true/false to the graph.",
            WaitsForCompletion = true)]
        public sealed class ReportResultAction : IAction
        {
            public void Execute(DialogueActionContext context)
                => Apply(context);

            public Task ExecuteAsync(DialogueActionContext context, CancellationToken ct)
            {
                Apply(context);
                return Task.CompletedTask;
            }

            private static void Apply(DialogueActionContext context)
            {
                string raw = (context.GetParameter(ParamResult, "true") ?? "true").Trim().ToLowerInvariant();
                if (raw == "neutral" || raw.Length == 0) context.SetNeutral();
                else context.SetResult(raw == "true" || raw == "1" || raw == "yes");
            }

            public void Cancel() { }
        }

        [DialogueAction(ExternalResult, Description = "Closes itself later through the API.",
            WaitsForCompletion = true, CompletesExternally = true)]
        public sealed class ExternalResultAction : IAction
        {
            public void Execute(DialogueActionContext context) => Start(context);

            public Task ExecuteAsync(DialogueActionContext context, CancellationToken ct)
            {
                Start(context);
                return Task.CompletedTask;
            }

            private static void Start(DialogueActionContext context)
            {
                float delay = context.GetFloat(ParamDelay, 0.1f);
                bool result = !string.Equals(context.GetParameter(ParamResult, "true"), "false",
                    StringComparison.OrdinalIgnoreCase);

                _ = CloseLater(context, delay, result);
            }

            private static async Task CloseLater(DialogueActionContext context, float delay, bool result)
            {
                try
                {

                    if (delay > 0f) await Task.Delay(TimeSpan.FromSeconds(delay)).ConfigureAwait(false);
                    context.SetResult(result);
                }
                catch (Exception ex)
                {
                    DialogueLogger.LogError("305", "ExternalResult error", ex.Message);
                    context.SetNeutral();
                }
            }

            public void Cancel() { }
        }

        [DialogueAction(QuestAccept, Description = "Accepts a quest.")]
        public sealed class QuestAcceptAction : IAction
        {
            public void Execute(DialogueActionContext context)
            {
                string questId = context.GetParameter(ParamQuestId);
                if (context.Quests == null || string.IsNullOrEmpty(questId))
                {
                    DialogueLogger.LogWarning($"{QuestAccept}: no quest service or questId.");
                    return;
                }
                context.Quests.Accept(questId);
            }

            public Task ExecuteAsync(DialogueActionContext context, CancellationToken ct)
            {
                Execute(context);
                return Task.CompletedTask;
            }

            public void Cancel() { }
        }

        [DialogueAction(QuestComplete, Description = "Completes a quest.")]
        public sealed class QuestCompleteAction : IAction
        {
            public void Execute(DialogueActionContext context)
            {
                string questId = context.GetParameter(ParamQuestId);
                if (context.Quests == null || string.IsNullOrEmpty(questId))
                {
                    DialogueLogger.LogWarning($"{QuestComplete}: no quest service or questId.");
                    return;
                }
                context.Quests.Complete(questId);
            }

            public Task ExecuteAsync(DialogueActionContext context, CancellationToken ct)
            {
                Execute(context);
                return Task.CompletedTask;
            }

            public void Cancel() { }
        }

        [DialogueAction(QuestFail, Description = "Fails a quest.")]
        public sealed class QuestFailAction : IAction
        {
            public void Execute(DialogueActionContext context)
            {
                string questId = context.GetParameter(ParamQuestId);
                if (context.Quests == null || string.IsNullOrEmpty(questId))
                {
                    DialogueLogger.LogWarning($"{QuestFail}: no quest service or questId.");
                    return;
                }
                context.Quests.Fail(questId);
            }

            public Task ExecuteAsync(DialogueActionContext context, CancellationToken ct)
            {
                Execute(context);
                return Task.CompletedTask;
            }

            public void Cancel() { }
        }

        [DialogueAction(QuestStep, Description = "Marks a quest objective done or pending.")]
        public sealed class QuestStepAction : IAction
        {
            public void Execute(DialogueActionContext context)
            {
                string questId = context.GetParameter(ParamQuestId);
                string stepId = context.GetParameter(ParamStepId);
                if (context.Quests == null || string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(stepId))
                {
                    DialogueLogger.LogWarning($"{QuestStep}: no quest service, questId or stepId.");
                    return;
                }
                context.Quests.SetStep(questId, stepId, context.GetBool(ParamDone, true));
            }

            public Task ExecuteAsync(DialogueActionContext context, CancellationToken ct)
            {
                Execute(context);
                return Task.CompletedTask;
            }

            public void Cancel() { }
        }

        [DialogueAction(Wait, Description = "Waits N seconds.", WaitsForCompletion = true)]
        public sealed class WaitAction : IAction
        {
            public void Execute(DialogueActionContext context)
                => DialogueLogger.LogWarning(
                    $"{Wait} on node '{context.Node?.Id}' should use wait: true, otherwise it has no effect.");

            public async Task ExecuteAsync(DialogueActionContext context, CancellationToken ct)
            {
                float seconds = context.GetFloat(ParamSeconds, 0f);
                if (seconds <= 0f) return;

                DialogueLogger.Log($"Wait: {seconds.ToString(CultureInfo.InvariantCulture)}s...");
                await Task.Delay(TimeSpan.FromSeconds(seconds), ct).ConfigureAwait(false);
                DialogueLogger.Log("Wait: done.");
            }

            public void Cancel() => DialogueLogger.Log("Wait cancelled.");
        }
    }
}
