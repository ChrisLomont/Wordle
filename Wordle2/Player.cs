namespace Wordle2;

class Player : IPlayer
{
    public string Start()
    {
        Console.WriteLine("Enter word, or blank for random");
        var ans = Console.ReadLine();

        return ans ?? String.Empty;
    }

    public void Result(uint result)
    {
        Util.Write(guess, result);
        Console.WriteLine();
    }

    string guess="";

    public string Get()
    {

        while (true)
        {
            guess = Console.ReadLine()??String.Empty;
            if (Words.HiddenWords.FirstOrDefault(w => w.Text == guess) != null)
                return guess;
            Console.Write(new string('\b', 5));
        }
    }
}