using System.Text;
using JeepneyFareCalculator.Application.Administrators;

namespace JeepneyFareCalculator.Api.Authentication;

/// <summary>
/// <c>dotnet run --project src/JeepneyFareCalculator.Api --no-launch-profile -- hash-password</c>
/// asks for the Administrator's password (twice, without showing it) and prints
/// the hash to store as <c>Administrator:PasswordHash</c>. See back-end/CLAUDE.md.
/// </summary>
internal static class HashPasswordCommand
{
    public const string Name = "hash-password";

    public static int Run()
    {
        var password = ReadPassword("Administrator password: ");
        if (password.Length == 0)
        {
            Console.Error.WriteLine("The password can't be empty.");
            return 1;
        }

        var confirmation = ReadPassword("Type it again: ");
        if (confirmation != password)
        {
            Console.Error.WriteLine("The passwords don't match.");
            return 1;
        }

        Console.WriteLine(PasswordHashing.Hash(password));
        return 0;
    }

    private static string ReadPassword(string prompt)
    {
        Console.Write(prompt);

        // Piped input (e.g. from a script) can't be hidden, so read it as a line.
        if (Console.IsInputRedirected)
        {
            var line = Console.ReadLine() ?? "";
            Console.WriteLine();
            return line;
        }

        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }

        Console.WriteLine();
        return password.ToString();
    }
}
