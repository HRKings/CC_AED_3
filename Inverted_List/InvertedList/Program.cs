using System;
using System.Data;
using System.Globalization;
using System.Text;

namespace InvertedList
{
    internal class Program
    {
        // Creates the stop words array
        private static string[] _stopWords = new string[] { " de ", " da ", " dos ", " das ", " e " };

        // Creates the list
        private static InvertedListImplementation inverList;

        /// <summary>
        /// Prints the options on the console
        /// </summary>
        private static void PrintOptions()
        {
            Console.WriteLine("Choose an options:\n 1 - Insert a name\n 2 - Search in the database\n-1 - Exit the program\n--------------");
        }

        private static void Main(string[] args)
        {
            // Creates an instance of the inverted list with the paths to the files
            inverList = new InvertedListImplementation("terms.db", "ids.db", "names.db");

            /*
            Try to add these values:
                1=Marcos Antônio de Oliveira
                2=José Marcos Resende
                3=Paula Oliveira
                4=Carlos José Antônio Souza
                5=José Carlos de Paula
            And to search for these terms:
                jose,paula
            */

            // The variable that will hold the option of th user
            int option = 0;
            string[] input = null;

            while (option != -1)
            {
                // Clears the console
                Console.Clear();

                // Print the options
                PrintOptions();
                Console.Write("> ");

                // Waits for user input
                try
                {
                    option = int.Parse(Console.ReadLine());
                }
                catch
                {
                    Console.WriteLine("It wasn't possible to read the number");
                    option = 0;
                }

                // Do something based on the user actions
                switch (option)
                {
                    case 1:
                        // Clears the console
                        Console.Clear();

                        // Prints the instructions
                        Console.WriteLine("Write a name in the format: ID=NAME");
                        Console.Write("> ");

                        // Divides the ID from the Name
                        input = Console.ReadLine().Split("=");

                        try
                        {
                            // Inserts the ID and Name
                            inverList.Create(int.Parse(input[0]), input[1]);
                        }
                        catch
                        {
                            // Writes the error on the console
                            Console.WriteLine($"Failed to insert ID = {input[0]}, NAME = {input[1]}");
                        }
                        break;

                    case 2:
                        // Clears the console
                        Console.Clear();

                        // Prints the instructions
                        Console.WriteLine("Write the search query:");
                        Console.Write("> ");

                        // Divides the terms
                        input = Normalize(Console.ReadLine()).ToLower().Split(" ");

                        // Iterates the results
                        foreach (int i in inverList.Read(input))
                        {
                            // Writes on the screen the IDs and the names associated with it
                            Console.WriteLine($"{i} - {inverList.QueryID(i)}");
                        }

                        Console.Write("Press any key to continue...");
                        Console.ReadKey();
                        break;

                    case -1:
                        break;

                    default:
                        // Clears the console
                        Console.Clear();

                        Console.WriteLine("Option not recognized\nPress any key to continue...");
                        Console.ReadKey();
                        break;
                }
            }
        }

        /// <summary>
        /// Normalizes the text (removes all accents and stop swords) and converts to lower case
        /// </summary>
        /// <param name="text">The text to normalize</param>
        /// <returns>The normalized text</returns>
        public static string Normalize(string text)
        {
            // Creates an string builder to rebuild the string after normalized
            StringBuilder sbReturn = new StringBuilder();

            // Converts the text to lower case and splits into a char array, normalizing each character
            char[] arrayText = text.ToLower().Normalize(NormalizationForm.FormD).ToCharArray();

            // For each letter in the array we compare
            foreach (char letter in arrayText)
            {
                // Selects the version of the character withou the accents
                if (CharUnicodeInfo.GetUnicodeCategory(letter) != UnicodeCategory.NonSpacingMark)
                {
                    // Puts the character back on the string
                    sbReturn.Append(letter);
                }
            }

            // Builds the normalized string
            string result = sbReturn.ToString();

            // Remove each stop word
            foreach (string word in _stopWords)
            {
                // Removes the stop word and updates the result string
                result = result.Replace(word, " ");
            }

            return result;
        }
    }
}