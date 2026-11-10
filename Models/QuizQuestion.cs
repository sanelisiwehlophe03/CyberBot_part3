namespace CyberBot.Models
{
    /// <summary>A multiple-choice or true/false quiz question with its explanation.</summary>
    public class QuizQuestion
    {
        public QuizQuestion(string text, string[] options, int correctIndex, string explanation)
        {
            Text = text;
            Options = options;
            CorrectIndex = correctIndex;
            Explanation = explanation;
        }

        public string Text { get; }
        public string[] Options { get; }
        public int CorrectIndex { get; }
        public string Explanation { get; }

        public bool IsTrueFalse => Options.Length == 2;
    }
}
