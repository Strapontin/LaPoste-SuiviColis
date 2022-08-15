using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace suivi_colis.Entities
{
    public class SuiviColisResponse
    {
        public Shipment shipment { get; set; }
    }

    public class Shipment
    {
        public List<Timeline> timeline { get; set; }
        [JsonPropertyName("event")]
        public List<EventColis> eventColis { get; set; }
    }

    public class Timeline
    {
        public int id { get; set; }
        public string shortLabel { get; set; }
        public DateTime date { get; set; }
    }

    public class EventColis
    {
        public string code { get; set; }
        public string label { get; set; }
        public DateTime date { get; set; }
    }
}
