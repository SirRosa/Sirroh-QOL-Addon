using System;
using Sirroh.Qol;
(string input, string expected, char variant)[] cases = {
    ("Hello", "Hello", '\0'),
    ("<mat=B>Hello", "Hello", 'B'),
    ("Srebro <mat=S>", "Srebro ", 'S'),
    ("<mat=G>Złoto", "Złoto", 'G'),
    ("<mat=R><color=red>Hej</color>", "<color=red>Hej</color>", 'R'),
    ("<mat=Y>Żółty", "Żółty", 'Y'),
    ("<MAT=b>Black", "Black", 'B'),
    ("<mat=X>Unknown", "<mat=X>Unknown", '\0'),
    ("<mat=B><mat=G>Last", "Last", 'G'),
    ("prefix<mat=B>suffix", "prefixsuffix", 'B'),
    ("<mat=BB>oops", "<mat=BB>oops", '\0'),
    ("<mat=B", "<mat=B", '\0'),
    ("abc<mat=B>def<mat=X>", "abcdef<mat=X>", 'B'),
};
foreach (var (input, expected, variant) in cases)
{
    string output = SignMaterialMarkup.Strip(input, out char result);
    if (output != expected || result != variant)
        throw new Exception($"Failed {input}: output {output}, variant {result}");
}
Console.WriteLine($"PASS: {cases.Length} isolated SignMaterialMarkup parsing scenarios");
