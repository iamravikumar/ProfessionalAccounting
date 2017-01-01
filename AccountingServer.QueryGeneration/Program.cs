﻿using System;
using System.IO;
using AccountingServer.Shell.Parsing;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace AccountingServer.QueryGeneration
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var inputStream = new StreamReader(Console.OpenStandardInput());
            while (true)
            {
                var input = new AntlrInputStream(inputStream.ReadLine());
                var lexer = new ShellLexer(input);
                var tokens = new CommonTokenStream(lexer);
                var parser = new ShellParser(tokens);
                IParseTree tree = parser.command();
                Console.WriteLine(tree.ToStringTree(parser));
            }
        }
    }
}