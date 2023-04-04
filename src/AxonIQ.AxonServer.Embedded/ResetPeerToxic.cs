using Toxiproxy.Net.Toxics;

namespace AxonIQ.AxonServer.Embedded;

public class ResetPeerToxic : ToxicBase
{
    public ResetPeerToxic() => this.Attributes = new ToxicAttributes();

    public ToxicAttributes Attributes { get; set; }

    public override string Type => "reset_peer";

    public class ToxicAttributes
    {
        public int Timeout { get; set; }
    }
}