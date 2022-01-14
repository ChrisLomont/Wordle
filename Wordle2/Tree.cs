namespace Wordle2
{
    // work on search of tree
    internal class Tree
    {
        public class Node
        {
            public override string ToString()
            {
                return $"{children.Count}, {possibleHiddenWords?.Count}";
            }

            // THREAD - must be done on single thread or dealt with differently
            public static int NodeCount;

            public Node()
            {
                NodeCount++;
                if ((NodeCount % 100000) == 0)
                    Console.WriteLine($"Node count {NodeCount}");
            }

            public int Depth; // root is 0
            public Knowledge? Knowledge;
            public List<Word>? possibleHiddenWords;
            public List<Node> children = new();
            public Node? parent;
        }

        public Node root;



        public void Build(IEnumerable<Word> possibleHiddenWords, params Word[] guessedOrder)
        {
            root = new Node { Knowledge = new(), possibleHiddenWords = new() };
            root.possibleHiddenWords.AddRange(possibleHiddenWords);
            var unprocessed = new Queue<Node>();
            unprocessed.Enqueue(root);
            var curDepth = 0;
            long nodesProcessed = 0, sameKnowledge = 0;

            while (unprocessed.Any())
            {
                var node = unprocessed.Dequeue();
                ++nodesProcessed;
                if (node.Depth > curDepth)
                {
                    curDepth = node.Depth;
                    Console.WriteLine($"Node depth to {curDepth} at {nodesProcessed}");
                }

                if (node.possibleHiddenWords.Count < 2)
                    continue; // done
                var guesses = Words.AllWords;
                if (node.Depth < guessedOrder.Length)
                    guesses = new() { guessedOrder[node.Depth] };

                foreach (var hiddenWord in node.possibleHiddenWords)
                    foreach (var guess in guesses)
                    {
                        var k = new Knowledge();
                        k.Copy(node.Knowledge);
                        k.Add(guess.Text, Util.Score(hiddenWord, guess));
                        var newPossible = k.Filter(node.possibleHiddenWords);
                        if (newPossible.Count < node.possibleHiddenWords.Count)
                        {
                            // see if any other kids have same knowledge, if so, this word points there too?
                            var same = false;
                            foreach (var c in node.children)
                                if (c.Knowledge.Same(k))
                                {
                                    same = true;
                                    ++sameKnowledge;
                                    break;
                                }

                            //same = false;
                            if (!same)
                            {

                                // made progress

                                var child = new Node
                                {
                                    Knowledge = k,
                                    possibleHiddenWords = newPossible,
                                    Depth = node.Depth + 1,
                                    parent = node
                                };

                                node.children.Add(child);
                                unprocessed.Enqueue(child);
                            }
                        }

                    }
            }
        }
    }
}
