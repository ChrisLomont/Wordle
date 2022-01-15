namespace Wordle2;

class Player : IPlayer
{
    public string Start()
    {
        Console.WriteLine("Choose the hidden word, or blank for random");
        var ans = Console.ReadLine();

        return ans ?? String.Empty;
    }

    public void Result(uint result)
    {
        Util.Write(guess, result);
        Console.WriteLine();
    }

    string guess = "";

    public string Get()
    {
        Console.WriteLine("Enter a guess:");
        while (true)
        {
            guess = Console.ReadLine() ?? String.Empty;
            if (Words.AllWords.FirstOrDefault(w => w.Text == guess) != null)
                return guess;
            Console.Write(new string('\b', 5));
        }
    }
}