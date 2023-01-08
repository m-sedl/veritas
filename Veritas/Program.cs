static void PrintGreeting()
{
    var color = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(@"
     _    __                   _    __                  
    | |  / /  ___     _____   (_)  / /_   ____ _   _____
    | | / /  / _ \   / ___/  / /  / __/  / __ `/  / ___/
    | |/ /  /  __/  / /     / /  / /_   / /_/ /  (__  ) 
    |___/   \___/  /_/     /_/   \__/   \__,_/  /____/
                    (powered by V#)
    ".Trim('\n'));
    Console.ForegroundColor = color;
}

PrintGreeting();
