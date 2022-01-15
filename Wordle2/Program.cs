// See https://aka.ms/new-console-template for more information

using System.Collections.Concurrent;
using System.Text;
using Wordle2;
using static Wordle2.Util;

Console.WriteLine("Wordle stuff");

CheckLetterAssumptions();
Testing.Test();
Testing.TestScore();

#if false
// for each start word, see what scores it gives against
Dictionary<uint, List<(Word answer, Word guess)>> scoreStarts = new();
foreach (var start in Words.AllWords)
foreach (var hidden in Words.HiddenWords)
{
    var score = Score(hidden, start);
    Knowledge k2 = new();
    k2.Add(start.Text, score);
    if (!scoreStarts.ContainsKey(score))
    {
        scoreStarts.Add(score,new());
        var c = scoreStarts.Count;
        if ((c%100) == 0)
            Console.WriteLine($"{c} so far, guess {start} against answer {hidden}");
    }
        scoreStarts[score].Add((hidden, start));
}

Console.WriteLine($"{scoreStarts.Count} unique score starts");
foreach (var s in scoreStarts.OrderByDescending(t=>t.Value.Count))
{
    Console.WriteLine($"{s.Key} from {s.Value.Count} items");
}

Knowledge.DumpHashInfo();
return;
#endif

//Tree tree = new();
//tree.Build(
//    //new List<Word>{Words.Lookup("brand"),Words.Lookup("strip")} // hidden set
//    Words.HiddenWords.Take(10) // hidden set
//    ,Words.Lookup("raise")
//    //,Words.Lookup("scone")
//    //,Words.Lookup("marks")
//);
//Console.WriteLine($"Number of nodes {Tree.Node.nodeCount}");
//return;


// start cutoff allows starting near last ending place
// end cutoff is last word cutoff to test
void ComputeStartingWords(int startCutoff = 0, int endCutoff = 200)
{

    // get best on avg and best on worst
    var (bestAvg, bestWorst) = BestFirstWord();
    // merge the top N of each
    var bestGuess = new List<Ans>();
    var nextIndex = 0; // take both these

    HashSet<string> used = new(); // track seen words to avoid double inserts

    // get at least N = endCutoff to test
    while (bestGuess.Count < endCutoff)
    {
        var a1 = bestAvg[nextIndex];
        var a2 = bestWorst[nextIndex];
        if (!used.Contains(a1.Word.Text))
        {
            bestGuess.Add(a1);
            used.Add(a1.Word.Text);
        }

        if (!used.Contains(a2.Word.Text))
        {
            bestGuess.Add(a2);
            used.Add(a2.Word.Text);
        }

        nextIndex++;
    }

    // remove initial
    bestGuess = bestGuess.Skip(startCutoff).ToList();

    foreach (var b in bestGuess.Take(10))
    {
        Console.WriteLine($"{b.Word.Text}");
    }

    Console.WriteLine($"Searching {bestGuess.Count + startCutoff} first words, starting at {startCutoff + 1}, used {nextIndex} from both best avg and lowest worst");

    ConcurrentDictionary<string, long[]> results = new();

#if false
    Parallel.For(0,bestGuess.Count, i =>
    {
            var start = bestGuess[i];
            var pass = i + 1;

            var startTime = Environment.TickCount;
            var (hist, maxword) = Play(new Robot(false, start.Word.Text, quiet: true), quiet: true); // run robot against PC
            var endTime = Environment.TickCount;
            var elapsed = TimeSpan.FromMilliseconds(endTime - startTime);

            DumpResult(hist, maxword, 
                prefix: $"({pass + startCutoff}/{startCutoff + bestGuess.Count}): {start.Word.Text} ({start.Avg:F3}) ({start.Worst}) => {elapsed} "
                );

            results.AddOrUpdate(start.Word.Text, hist, (_,_)=>throw new Exception($"Already added {start.Word.Text}"));

        }
        );
#else
    int pass = 0;
    foreach (var start in bestGuess)
    {
        ++pass;
        var startTime = Environment.TickCount;
        Console.Write($"({pass + startCutoff}/{startCutoff + bestGuess.Count}): {start.Word.Text} ({start.Avg:F3}) ({start.Worst}) => ");
        var (hist, maxWord) = Play(
            new Robot(false, 
                quiet: true, 
                multiThreaded:true,
                startWords: start.Word.Text
                ), quiet: true); // run robot against PC
        var endTime = Environment.TickCount;
        var elapsed = TimeSpan.FromMilliseconds(endTime - startTime);
        Console.Write($"{elapsed} ");
        DumpResult(hist, maxWord);
        results.AddOrUpdate(start.Word.Text, hist, (_, _) => throw new Exception($"Already added {start.Word.Text}"));
    }
#endif
}



//Knowledge k = new();
//k.Add("raise",ToScore(Info.Misplaced, Info.Perfect, Info.Unused, Info.Unused, Info.Unused));
//var f = k.Filter(Words.HiddenWords);
//Console.WriteLine($"{f.Count}");

//return;

// thing to do
Console.WriteLine("Enter command:");
Console.WriteLine("0: run robot (optional words on command line to start play)");
Console.WriteLine("1: play against the computer");
Console.WriteLine("2: Helper to assist playing online");
Console.WriteLine("3: Analyze two word combos");
Console.WriteLine("4: best first word");
Console.WriteLine("5: Some per letter stats");
Console.WriteLine("6: search start words");
Console.WriteLine("See code for more");
var line = Console.ReadLine() ?? "";
var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
var task = words[0];
switch (task)
{
    case "0":
        Play(new Robot(true, multiThreaded:true, startWords:words.Skip(1).ToArray())); // run robot against PC
        break;
    case "1":
        Play(); // play against PC
        break;
    case "2":
        GameHelper(); // interactive game helper
        break;
    case "3":
        Analyze(); // check which 2 word combos end up with low unknowns after
        break;
    case "4":
        // compute best first word
        BestFirstWord();
        break;
    case "5":
        // letter counts
        TallyLetters();
        break;
    case "6":
        // try various starting words
        ComputeStartingWords(0, 500);
        break;

}

return; // done!

// analyze ideas
void Analyze()
{
    int bestBest = int.MaxValue, bestWorst = Int32.MaxValue;
    float bestAvg = float.MaxValue;
    // look over each two words, and see the best, avg, and worst it reduces the world to
    var hiddenWords = Words.HiddenWords;
    var allLen = hiddenWords.Count;
    object locker = new object();
    long total = (allLen * (allLen + 1)) / 2;
    long finished = 0;
    Parallel.For(0, allLen, i =>
         {
            //for (var i = 0; i < allLen; ++i)
            for (var j = i + 1; j < allLen; ++j)
            {
                var w1 = hiddenWords[i];
                var w2 = hiddenWords[j];
                var (best, avg, worst) = Stats(w1, w2);
                lock (locker)
                {
                    if (best < bestBest)
                    {
                        bestBest = best;
                        Console.WriteLine($"\n{w1},{w2} -> {best},{avg},{worst}");
                    }

                    if (avg < bestAvg)
                    {
                        bestAvg = avg;
                        Console.WriteLine($"\n{w1},{w2} -> {best},{avg},{worst}");
                    }

                    if (worst < bestWorst)
                    {
                        bestWorst = worst;
                        Console.WriteLine($"\n{w1},{w2} -> {best},{avg},{worst}");
                    }

                    ++finished;
                    if ((finished % 500) == 0)
                    {
                        var pct = finished * 100.0 / total;
                        Console.Write($"{pct:F2}% ");
                    }
                }
            }
         }
    );

    (int best, float avg, int worst) Stats(Word w1, Word w2)
    {
        List<int> counts = new();
        foreach (var w in Words.HiddenWords)
        {
            var k = new Knowledge();
            k.Add(w1.Text, Score(w.Text, w1.Text));
            k.Add(w2.Text, Score(w.Text, w2.Text));
            counts.Add(Words.HiddenWords.Where(k.WordPossible).Count());
        }

        var best = counts.Min();
        var worst = counts.Max();
        var total1 = counts.Sum();
        var avg = (float)total1 / counts.Count;
        return (best, avg, worst);
    }

}

// interactive deduce word
void GameHelper()
{
    var k = new Knowledge();
    while (true)
    {
        Console.WriteLine("Enter word then space then 5 letter result sequence using 'G' - green correct, 'Y' yellow misplaced, '.' gray unused, blank to start over");
        var lineRead = Console.ReadLine();
        if (string.IsNullOrEmpty(lineRead))
        {
            k = new Knowledge();
            continue;
        }
        if (lineRead.Length != 11)
            continue;
        uint score = 0;
        var text = lineRead.Substring(0, 5).ToLower();
        var resultTxt = lineRead.Substring(6, 5).ToUpper();
        for (var i = 0; i < resultTxt.Length; ++i)
        {
            var t = resultTxt[i] switch
            {
                '.' => Info.Unused,
                'Y' => Info.Misplaced,
                'G' => Info.Perfect,
                _ => throw new NotImplementedException()
            };
            score += ((uint)(t) << (2 * i));
        }
        k.Add(text, score);
        var left = k.Filter(Words.HiddenWords);
        var ans = ScoreAll(k, verbose: false, guessWordStyle: 2);

        Console.Write($"\n{left.Count} ");
        for (var i = 0; i < left.Count; ++i)
        {
            Console.Write($"{left[i].Text} ");
            if (i > 20) break;
        }
        Console.WriteLine();

        Console.Write($"\n{ans.Count} ");
        for (var i = 0; i < ans.Count; ++i)
        {
            Console.Write($"{ans[i].Word.Text} ({ans[i].Worst}) ");
            if (i > 40) break;
        }
        Console.WriteLine();

    }
}

// play a game
(long[] histogram, string maxWord) Play(IPlayer? player = null, bool quiet = false)
{
    player ??= new Player();

    var histogram = new long[20];

    var r = new Random(1234);
    //int min = 1000, max = -1;
    int max = -1;
    string maxWord = "";
    while (true)
    {
        var ans = player.Start();
        if (ans == "quit") break;

        if (string.IsNullOrEmpty(ans))
            ans = Words.HiddenWords[r.Next(Words.HiddenWords.Count)].Text;
        if (!quiet)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(ans);
            Console.ForegroundColor = ConsoleColor.White;
        }

        string guess = "";
        int pass = 0;
        while (guess != ans)
        {
            guess = player.Get();

            var arr = Score(ans, guess);

            player.Result(arr);

            ++pass;
        }


        histogram[pass]++;
        if (max < pass)
        {
            max = Math.Max(max, pass);
            maxWord = ans;
        }

        if (!quiet)
        {
            Console.Write(
                $"Guessed {ans} in {pass} moves, ");
            DumpResult(histogram, maxWord);
        }

    }
    return (histogram, maxWord);
}

void DumpResult(long[] histogram, string maxWord, string prefix = "")
{
    int min = int.MaxValue, max = int.MinValue;
    var gameCount = histogram.Sum();
    long tt = 0;
    for (var i = 0; i < histogram.Length; ++i)
    {
        tt += histogram[i] * i;
        if (histogram[i] != 0)
        {
            min = Math.Min(min, i);
            if (max < i)
            {
                max = Math.Max(max, i);
            }
        }
    }

    var avg = (double)tt / gameCount;
    var sb = new StringBuilder(prefix);
    sb.Append($"{gameCount} games, min {min}, max {max} ({maxWord}), avg {avg:F4}: ");
    for (var i = 0; i < histogram.Length; ++i)
    {
        if (histogram[i] != 0)
            sb.Append($"[{i}] = {histogram[i]}({100.0 * histogram[i] / gameCount:F2}%) ");
    }

    Console.WriteLine(sb.ToString());
}