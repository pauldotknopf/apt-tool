using System;
using System.IO;
using System.Text;
using AptTool.Workspace;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace AptTool.Commands
{
    public class Man
    {
        [Verb("man", HelpText = "Display some helpful information in getting started.")]
        public class Command
        {
            
        }

        public static int Run(Command command)
        {
            var sp = Program.BuildServiceProvider(new Program.CommonOptions());

            using (var stream = typeof(Man).Assembly.GetManifestResourceStream("AptTool.Resources.Manual.txt"))
            {
                if (stream == null)
                {
                    throw new Exception("Can't find Manual.txt. resource.");
                }
                using (var streamReader = new StreamReader(stream))
                {
                    var skipping = false;
                    while (!streamReader.EndOfStream)
                    {
                        var line = streamReader.ReadLine();
                        if (string.IsNullOrEmpty(line))
                        {
                            Console.WriteLine();
                            continue;
                        }

                        if (line == "--------------")
                        {
                            skipping = !skipping;
                        }

                        if (skipping)
                        {
                            Console.WriteLine(line);
                            continue;
                        }
                        
                        var words = line.Split(" ");

                        var currentCount = 0;
                        var currentIndex = 0;
                        var currentLine = "";
                        while (currentIndex < words.Length)
                        {
                            var word = words[currentIndex];
                            if (currentCount + word.Length > 75)
                            {
                                // This would overflow.
                                Console.WriteLine(currentLine);
                                currentLine = "";
                                currentCount = 0;
                                continue;
                            }

                            if (!string.IsNullOrEmpty(currentLine))
                            {
                                currentLine += $" {word}";
                                currentCount += word.Length + 1;
                            }
                            else
                            {
                                currentLine += word;
                                currentCount += word.Length;
                            }
                            
                            currentIndex += 1;
                        }
                        
                        Console.WriteLine(currentLine);
                    }
                }
            }
            
            return 0;
        }
    }
}