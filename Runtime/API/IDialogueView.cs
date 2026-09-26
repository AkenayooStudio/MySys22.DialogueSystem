namespace MySys22.Engine.API
{

    public interface IDialogueView
    {

        void SetPanelVisible(bool visible);

        bool IsPanelVisible { get; }

        void ShowText(string speaker, string text);

        void ShowText(string text);

        void SetSpeaker(string speaker);

        void SetText(string text);

        void HideText();

        void HideChoices();
    }
}
