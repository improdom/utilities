private static List<string> SplitTopLevelArguments(string input)
{
    var result = new List<string>();
    var current = new StringBuilder();
    int nested = 0;

    foreach (char c in input)
    {
        if (c == '(') nested++;
        else if (c == ')') nested--;

        if (c == ',' && nested == 0)
        {
            result.Add(current.ToString());
            current.Clear();
        }
        else
        {
            current.Append(c);
        }
    }

    if (current.Length > 0)
        result.Add(current.ToString());

    return result;
}
