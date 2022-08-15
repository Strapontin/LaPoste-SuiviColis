using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using suivi_colis.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using DSharpPlus.Entities;

namespace suivi_colis.Commands
{
    public class GeneralModule : BaseCommandModule
    {
        readonly static HttpClient _client = new();
        readonly static List<ParcelToTrack> _parcelsToTrack = new();
        readonly static List<string> _codesStopTracking = new() { "MD2", "ND1", "AG1", "RE1", "DI1", "DI2", "DI0" };
        Timer _timer = new Timer(OnTimedCheckTrackedParcels, null, 0, 10 * 10000);// timer de 10 minutes

        public GeneralModule()
        { }

        public override Task BeforeExecutionAsync(CommandContext ctx)
        {
            if (_client.BaseAddress == null)
            {
                _client.BaseAddress = new Uri(@"https://api.laposte.fr/");
                _client.DefaultRequestHeaders.Accept.Clear();
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _client.DefaultRequestHeaders.Add("X-Okapi-Key", Environment.GetEnvironmentVariable("LAPOSTE_KEY_SUIVI_COLIS"));
            }

            return Task.CompletedTask;
        }

        [Command("tuto")]
        [Description("Décrit le fonctionnement du bot")]
        public async Task TrackCommand(CommandContext ctx)
        {
            string msg = "Hello !\nJe suis la pour vous notifier de l'avancement de votre colis livré avec LaPoste.\n";
            msg += "Ne partagez pas votre numéro de colis dans un salon du serveur, d'autres utilisateurs pourraient l'utiliser de façon malveillante.\n";
            msg += "Envoyer moi en message privé '!track (numéro_de_colis)' pour que je vous tienne informer de l'avancement de votre colis.\n";
            msg += "Afin de ne pas trop encombrer le réseau de la poste, je ne fais que trois mises à jour dans la journée : 9h, 12h et 18h.\n";
            msg += "J'enregistre votre numéro de colis en mémoire pour vérifier son état périodiquement, mais nul part ailleurs. Personne ne peut le voir.\n";
            msg += "Si vous souhaitez voir le code source : https://github.com/Strapontin/LaPoste-SuiviColis";

            await ctx.RespondAsync(msg);
        }

        [Command("track")]
        [Description("Ajoute le suivi d'un colis")]
        public async Task TrackCommand(CommandContext ctx, string parcelCode)
        {
            if (!ctx.Channel.IsPrivate)
            {
                await ctx.Message.DeleteAsync();
                await ctx.RespondAsync($"Attention {ctx.Message.Author.Mention}, la commande 'track' doit être effectué en envoyant un message privé au bot !");
                return;
            }

            if (_parcelsToTrack.Any(ptt => ptt.ParcelCode == parcelCode && ptt.DiscordChannel == ctx.Channel))
            {
                await ctx.RespondAsync($"Ce colis est déjà suivi.");
                return;
            }

            CheckParcel(ctx.Channel, parcelCode);
        }

        [Command("untrack")]
        [Description("Annule le suivi d'un colis")]
        public async Task UntrackCommand(CommandContext ctx, string parcelCode)
        {
            if (!ctx.Channel.IsPrivate)
            {
                await ctx.Message.DeleteAsync();
                await ctx.RespondAsync($"Attention {ctx.Message.Author.Mention}, la commande 'untrack' doit être effectué en envoyant un message privé au bot !");
                return;
            }

            if (_parcelsToTrack.Any(ppt => ppt.ParcelCode == parcelCode && ppt.DiscordChannel == ctx.Channel))
            {
                _parcelsToTrack.RemoveAll(ppt => ppt.ParcelCode == parcelCode);
                await ctx.RespondAsync($"Ce colis n'est dorénavent plus suivi.");
            }
            else
            {
                await ctx.RespondAsync($"Ce code n'est pas suivi ou ne vous appartient pas.");
            }
        }


        private static void OnTimedCheckTrackedParcels(object o)
        {
            foreach (var parcel in _parcelsToTrack.Where(parcel => parcel.NextDateTimeToTrack < DateTime.Now))
            {
                CheckParcel(parcel.DiscordChannel, parcel.ParcelCode);
            }
        }


        private static async void CheckParcel(DiscordChannel channel, string parcelCode)
        {
            HttpResponseMessage response = await _client.GetAsync($"suivi/v2/idships/{parcelCode}?lang=fr_FR");

            if (response.IsSuccessStatusCode)
            {
                string res = await response.Content.ReadAsStringAsync();

                SuiviColisResponse suiviColis = JsonSerializer.Deserialize<SuiviColisResponse>(res);
                var eventColis = suiviColis.shipment.eventColis;
                var mostRecentUpdate = eventColis.First(ec => ec.date == eventColis.Max(ec2 => ec2.date));

                // If parcel is already tracked and the last update was already send
                if (_parcelsToTrack.Any(ptt => ptt.ParcelCode == parcelCode && 
                                               ptt.LastEventParcel.code == mostRecentUpdate.code &&
                                               ptt.DiscordChannel == channel))
                {
                    UpdateNextDateTimeToTrack(parcelCode, mostRecentUpdate);
                    return;
                }
                
                // Untrack when parcel update hits specific code
                if (_codesStopTracking.Contains(mostRecentUpdate.code))
                {
                    _parcelsToTrack.RemoveAll(ppt => ppt.ParcelCode == parcelCode);
                    await channel.SendMessageAsync($"Le bot ne suis plus le colis car l'état du colis a le code '{mostRecentUpdate.code}'.");
                }
                // Tracks if parcelCode isn't already track and last update doesn't hit specific code
                else if (!_parcelsToTrack.Any(ppt => ppt.ParcelCode == parcelCode))
                {
                    _parcelsToTrack.Add(new ParcelToTrack(parcelCode, mostRecentUpdate, channel));
                }

                // Updates parcel code
                UpdateNextDateTimeToTrack(parcelCode, mostRecentUpdate);

                await channel.SendMessageAsync($"Etat de la commande '{parcelCode}' : \n\n({mostRecentUpdate.code}) {mostRecentUpdate.label}");
            }
            else
            {
                await channel.SendMessageAsync($"Une erreur est survenue : {await response.Content.ReadAsStringAsync()}");
            }
        }

        private static void UpdateNextDateTimeToTrack(string parcelCode, EventColis mostRecentUpdate)
        {
            var parcel = _parcelsToTrack.First(ptt => ptt.ParcelCode == parcelCode);
            parcel.LastEventParcel = mostRecentUpdate;
            parcel.UpdateNextDateTimeToTrack();
        }
    }
}
