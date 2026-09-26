using System;

namespace MySys22.DialogueEngine.Core
{

    [Serializable]
    public sealed class ChoiceOption
    {

        public int Index;

        public string Text;

        public string Next;

        public string OnChosen;

        public int TextDid;

        public string Speaker;

        public ChoiceData Source;

        public override string ToString() => $"{Index}: {Text}";
    }
}
