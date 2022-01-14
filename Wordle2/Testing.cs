using System.Diagnostics;

namespace Wordle2
{
    public static class Testing
    {
        public static void Test()
        {


            var i = 0;
            while (i < Games.Length)
            {
                i = TestGame(i);
            }
        }

        // test game starting at index, return new index, throw on error
        static int TestGame(int index)
        {
            var answer = Games[index++];
            while (index < Games.Length && Games[index].Length == 10)
            {
                var txt = Games[index++];
                var guess = txt.Substring(0, 5);
                var ans = txt.Substring(5, 5);
                var sc = Util.Score(answer,guess);
                var ans2 = "";
                for (var i = 0; i < 5; ++i)
                {
                    var t = (Info)(((sc>>(i*2))&3));
                    ans2 += t switch
                    {
                        Info.Unused => '.',
                        Info.Perfect => 'G',
                        Info.Misplaced => 'Y',
                        _ => throw new Exception("")
                    };

                }
                Trace.Assert(ans==ans2);
            }

            return index;

        }

        // list of games, first is answer, next are clues returned
        // from web games
        static readonly string[] Games = new[]
        {

            // ReSharper disable All StringLiteralTypo
            "crank","fruit.G...","track.GGYG","crackGGG.G", 
            "rebus", "arise.Y.YY", "routeG.Y.Y", "rulesGY.YG",
            "craze","track.GGY.","crampGGG..","crazyGGG.",
            "today","stood.YY.Y","dotesYGY..","toadsGGYY.",
            "crank","quick...YG","stack..GYG","recceY.Y..","clackG.G.G",
            // ReSharper enable All StringLiteralTypo
        };

        public static void TestScore()
        {
            // from Norvig https://github.com/norvig/pytudes/blob/main/ipynb/jotto.ipynb
            Trace.Assert(ReplyFor("treat", "truss") == "GG..." && ReplyFor("truss", "treat") == "GG...");
            Trace.Assert(ReplyFor("stars", "traps") == ".YGYG" && ReplyFor("traps", "stars") == "YYG.G");
            Trace.Assert(ReplyFor("palls", "splat") == "YYG.Y" && ReplyFor("splat", "palls") == "YYGY.");
            Trace.Assert(ReplyFor("banal", "apple") == ".Y..Y" && ReplyFor("apple", "banal") == "Y..Y.");
            Trace.Assert(ReplyFor("banal", "mania") == ".GGY." && ReplyFor("mania", "banal") == ".GG.Y");
            Trace.Assert(ReplyFor("epees", "geese") == "Y.GYY" && ReplyFor("geese", "epees") == ".YGYY");
            Trace.Assert(ReplyFor("wheee", "peeve") == "..GYG" && ReplyFor("peeve", "wheee") == ".YG.G");

            string ReplyFor(string guess, string target)
            {
                var ans = Util.Score(target, guess);
                var txt = "";
                for (var i = 0; i < 5; ++i)
                {
                    var t = (Info)((ans >> (2 * i)) & 3);
                    txt += t switch
                    {
                        Info.Perfect => "G",
                        Info.Misplaced => "Y",
                        Info.Unused => ".",
                        _ => throw new Exception("Unknown")
                    };
                }
                //Console.WriteLine($"{guess},{target} => {txt}");
                return txt;
            }

        }



    }
}
