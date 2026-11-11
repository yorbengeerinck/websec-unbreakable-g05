namespace Unbreakable.Api;

public static class ApiHelpers
{
    // Ontsnapt de LIKE-jokertekens zodat een zoekterm letterlijk gezocht wordt.
    public static string EscapeLike(string value)
    {
        return value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }
}
