namespace Wordle2
{
    /// <summary>
    /// Track word for matching
    /// </summary>
    public class Word
    {
        public long Index { get; }

        public override string ToString() => Text;

        public Word(string text, long index)
        {
            this.Text = text;
            for (var i = 0; i < text.Length; ++i)
            {

                var j = text[i] - 'a';
                CorrectIndices.Set(5 * j + i, 1);
                UsedBitFlags |= 1U << j;
            }

            Index = index;
        }

        // bit i + 5*j set for letter 'a'+j in slot i
        public Bits CorrectIndices = new();
        // bit i set if letter 'a'+i used
        public uint UsedBitFlags { get; }

        public string Text { get; }


    }
}
