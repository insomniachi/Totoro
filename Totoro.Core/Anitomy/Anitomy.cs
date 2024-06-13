namespace Anitomy;


public class Anitomy
{

    public static IEnumerable<Element> Parse(string filename, Options options)
    {
        var elements = new List<Element>(32);
        var tokens = new List<Token>();

        /** remove/parse extension */
        var fname = filename;
        if (options.ParseFileExtension)
        {
            var extension = "";
            if (RemoveExtensionFromFilename(ref fname, ref extension))
            {
                elements.Add(new Element(ElementCategory.FileExtension, extension));
            }
        }

        /** set filename */
        if (string.IsNullOrEmpty(filename))
        {
            return elements;
        }
        elements.Add(new Element(ElementCategory.FileName, fname));

        /** tokenize */
        var isTokenized = new Tokenizer(fname, elements, options, tokens).Tokenize();
        if (!isTokenized)
        {
            return elements;
        }
        new Parser(elements, options, tokens).Parse();
        return elements;
    }

    public static IEnumerable<Element> Parse(string filename)
    {
        return Parse(filename, new Options());
    }

    private static bool RemoveExtensionFromFilename(ref string filename, ref string extension)
    {
        int position;
        if (string.IsNullOrEmpty(filename) || (position = filename.LastIndexOf('.')) == -1)
        {
            return false;
        }

        /** remove file extension */
        extension = filename.Substring(position + 1);
        if (extension.Length > 4 || !extension.All(char.IsLetterOrDigit))
        {
            return false;
        }

        /** check if valid anime extension */
        var keyword = KeywordManager.Normalize(extension);
        if (!KeywordManager.Contains(ElementCategory.FileExtension, keyword))
        {
            return false;
        }

        filename = filename.Substring(0, position);
        return true;
    }
}