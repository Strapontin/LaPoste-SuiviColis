using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace suivi_colis.Entities
{
    public class ParcelToTrack
    {
        private readonly List<int> _hoursToTrack = new() { 9, 12, 18 };

        public string ParcelCode { get; set; }
        public EventColis LastEventParcel { get; set; }
        public DiscordChannel DiscordChannel { get; set; }
        public DateTime NextDateTimeToTrack { get; set; }


        public ParcelToTrack(string parcelCode, EventColis eventColis, DiscordChannel discordChannel)
        {
            ParcelCode = parcelCode;
            LastEventParcel = eventColis;
            DiscordChannel = discordChannel;

            UpdateNextDateTimeToTrack();
        }

        public void UpdateNextDateTimeToTrack()
        {
            int hourNow = DateTime.Now.Hour;
            DateTime now = DateTime.Now;
            int hourToTrack = _hoursToTrack
                .Where(h => h > hourNow)
                .OrderBy(h => h)
                .FirstOrDefault();

            NextDateTimeToTrack = hourToTrack == 0 ?
                new DateTime(now.Year, now.Month, now.Day + 1, _hoursToTrack.First(), 0, 0) :
                new DateTime(now.Year, now.Month, now.Day, hourToTrack, 0, 0);
        }
    }
}
