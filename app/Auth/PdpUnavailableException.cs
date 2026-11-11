namespace Unbreakable.Auth;

// De policy decision point (OpenFGA) is niet bereikbaar of gaf een fout. Wordt vertaald naar 503
// (fail closed): nooit toegang verlenen als de beslissing niet gehaald kan worden.
public sealed class PdpUnavailableException : Exception
{
    public PdpUnavailableException(string message, Exception inner = null)
        : base(message, inner)
    {
    }
}
