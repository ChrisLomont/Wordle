namespace Wordle2;

// Robot must play on a single thread
class Robot : IPlayer
{
    Knowledge k = new();
    int pass;
    readonly bool verbose;

    int hiddenIndex;

    readonly string [] startWords;
    readonly bool quiet;
    readonly bool multiThreaded;
    
    public Robot(bool verbose, bool quiet = false, bool multiThreaded = false, params string [] startWords)
    {
        this.verbose = verbose;
        this.startWords = startWords;
        guess = String.Empty;
        this.quiet = quiet;
        this.multiThreaded = multiThreaded;

    }

    public string Start()
    {
        if (hiddenIndex >= Words.HiddenWords.Count)
        {
            return "quit";
        }
        if (!quiet)
            Console.Write($"Game {hiddenIndex + 1}/{Words.HiddenWords.Count}: ");
        k = new();
        pass = 0;
        if (verbose)
            Console.WriteLine("Robot start");
        return Words.HiddenWords[hiddenIndex++].Text;// do them all till crash
    }

    // cache second word based on response to first, speeds up
    readonly Dictionary<uint, string> cacheTwo = new();

    string guess; // last guess
    uint result; // last result
    public string Get()
    {
        //var starts = new[]{ "raise"};
        //var starts = new[]{ "stole"};
        //var starts = new[] { "roast" };
        // ReSharper disable once CommentTypo
        //var starts = new[] { "roate" };
        var starts = new[] { "trace" };
        //var starts = new[] { "soare" };
        // ReSharper disable once CommentTypo
        //var starts = new[] { "nosey", "trail" };
        //var starts = new[] { "trice", "salon" };
        //var starts = new[] { "tapir", "close" };
        //var starts = new[] { "salon", "tripe" };

        if (startWords.Length > 0)
            starts = startWords;

        if (pass < starts.Length) guess = starts[pass]; // todo - find best?
        else
        {
            guess = ""; // mark unused
            var hashRound = pass == starts.Length && starts.Length == 1;
            //var hash = Hash(result);
            if (hashRound)
            {
                if (cacheTwo.ContainsKey(result))
                    guess = cacheTwo[result];
            }

            if (guess == "")
            {
                var left = Util.ScoreAll(
                    knowledge:k, 
                    guessWordStyle:2,
                    multiThreaded:multiThreaded,
                    verbose:false,
                    sortOnWorst:false
                    );
                guess = left[0].Word.Text;
                if (hashRound)
                    cacheTwo.Add(result, guess);
            }
        }

        ++pass;
        if (verbose)
            Console.Write($"{pass}: robot guesses {guess} -> ");

        return guess;

        //ulong Hash(Info [] arr)
        //{
        //    ulong hash = 0;
        //    foreach (var i in arr)
        //        hash = 5 * hash + (ulong)i;
        //    return hash;
        //}
    }

    public void Result(uint result1)
    {
        this.result = result1; // this for caching
        k.Add(guess, result1);
        if (verbose)
        {
            Util.Write(guess, result1);

            var left = k.Filter(Words.HiddenWords).Count;
            Console.Write($", {left} left, ");
            Knowledge.DumpHashInfo();
        }
    }
}