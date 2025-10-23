using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SpotifyAPI.Web;

namespace MyFirstProject
{
    internal class MusicManager
    {
        private static SpotifyClient _spotify;
        private static AuthorizationCodeTokenResponse _auth;

        // 1) Set these: register RedirectUri in Spotify Dashboard (exact match)
        private static readonly string ClientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID");
        private const string RedirectUri = "https://127.0.0.1:4002/callback/";
        private static string _pkceVerifier;

        private static string[] Scopes = new[]
        {
            "user-read-playback-state",
            "user-modify-playback-state",
            "user-read-currently-playing",
            "user-read-private"
        };

        public static async Task ShowMusicControlMenuAsync()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            if (_spotify == null)
                await EnsureAuthenticatedAsync();

            bool running = true;
            while (running)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("╔════════════════════════════════════════════════╗");
                Console.WriteLine("║                Spotify Music Control          ║");
                Console.WriteLine("╚════════════════════════════════════════════════╝");
                Console.ResetColor();
                Console.WriteLine("1. Show Current Song");
                Console.WriteLine("2. Play/Pause");
                Console.WriteLine("3. Next Song");
                Console.WriteLine("4. Previous Song");
                Console.WriteLine("5. Search & Play Song");
                Console.WriteLine("6. Exit Music Control");
                Console.Write("\nSelect an option (1-6): ");

                var input = Console.ReadLine();
                try
                {
                    switch (input)
                    {
                        case "1":
                            await ShowCurrentSongAsync();
                            break;
                        case "2":
                            if (!await EnsurePlaybackPrereqsAsync()) break;
                            await TogglePlayPauseAsync();
                            break;
                        case "3":
                            if (!await EnsurePlaybackPrereqsAsync()) break;
                            await _spotify.Player.SkipNext();
                            Console.WriteLine("\nSkipped to next song. Press any key to return...");
                            Console.ReadKey(true);
                            break;
                        case "4":
                            if (!await EnsurePlaybackPrereqsAsync()) break;
                            await _spotify.Player.SkipPrevious();
                            Console.WriteLine("\nWent to previous song. Press any key to return...");
                            Console.ReadKey(true);
                            break;
                        case "5":
                            if (!await EnsurePlaybackPrereqsAsync()) break;
                            await SearchAndPlaySongAsync();
                            break;
                        case "6":
                            running = false;
                            break;
                        default:
                            Console.WriteLine("Invalid option. Press any key to continue...");
                            Console.ReadKey(true);
                            break;
                    }
                }
                catch (APIUnauthorizedException)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nSession expired. Re-authenticating...");
                    Console.ResetColor();
                    _spotify = null;
                    await EnsureAuthenticatedAsync();
                }
                catch (APIException apiEx)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nSpotify error: {apiEx.Message}");
                    Console.ResetColor();
                    Console.WriteLine("Tip: You need a Spotify Premium account and an active device.");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nUnexpected error: {ex.Message}");
                    Console.ResetColor();
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                }
            }
        }

        private static async Task EnsureAuthenticatedAsync()
        {
            if (string.IsNullOrWhiteSpace(ClientId))
                throw new InvalidOperationException("Spotify ClientId is not set. Set the SPOTIFY_CLIENT_ID environment variable and register the Redirect URI exactly: https://localhost:4002/callback/");

            var pkce = PKCEUtil.GenerateCodes();
            _pkceVerifier = pkce.verifier;

            var login = new LoginRequest(new Uri(RedirectUri), ClientId, LoginRequest.ResponseType.Code)
            {
                CodeChallenge = pkce.challenge,
                CodeChallengeMethod = "S256",
                Scope = Scopes
            }.ToUri();

            Console.WriteLine("Opening browser for Spotify login...");
            Console.WriteLine($"ClientId: {ClientId}");
            Console.WriteLine($"RedirectUri: {RedirectUri}");
            Console.WriteLine("Authorize URL: " + login);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(login.ToString()) { UseShellExecute = true });

                using (var http = new HttpListener())
            {
                http.Prefixes.Add(RedirectUri);
                try
                {
                    http.Start();
                }
                catch (HttpListenerException)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nFailed to start local listener.");
                    Console.WriteLine("On Windows, reserve URL and bind an SSL certificate, then try again.");
                    Console.ResetColor();
                    throw;
                }

                var ctx = await http.GetContextAsync();
                var code = ctx.Request.QueryString["code"];
                var error = ctx.Request.QueryString["error"];

                var responseHtml = string.IsNullOrEmpty(error)
                    ? "<html><body><h2>Spotify login successful!</h2>You can close this window.</body></html>"
                    : $"<html><body><h2>Spotify login failed: {WebUtility.HtmlEncode(error)}</h2></html>";

                var buf = Encoding.UTF8.GetBytes(responseHtml);
                ctx.Response.ContentLength64 = buf.Length;
                using (var os = ctx.Response.OutputStream) { os.Write(buf, 0, buf.Length); }
                http.Stop();

                if (!string.IsNullOrEmpty(error))
                    throw new Exception("Spotify authorization failed: " + error);

                if (string.IsNullOrEmpty(code))
                    throw new Exception("Spotify authorization code missing.");

                var oauth = new OAuthClient();
                var pkceTokenResponse = await oauth.RequestToken(new PKCETokenRequest(ClientId, code, new Uri(RedirectUri), _pkceVerifier));
                _auth = new AuthorizationCodeTokenResponse
                {
                    AccessToken = pkceTokenResponse.AccessToken,
                    TokenType = pkceTokenResponse.TokenType,
                    ExpiresIn = pkceTokenResponse.ExpiresIn,
                    Scope = pkceTokenResponse.Scope,
                    RefreshToken = pkceTokenResponse.RefreshToken,
                    CreatedAt = pkceTokenResponse.CreatedAt
                };
                _spotify = new SpotifyClient(_auth.AccessToken);
            }
        }

        private static async Task<bool> EnsurePlaybackPrereqsAsync()
        {
            // Must be Premium to control playback
            var me = await _spotify.UserProfile.Current();
            if (!string.Equals(me.Product, "premium", StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nPlayback control requires a Spotify Premium account.");
                Console.ResetColor();
                Console.WriteLine("Press any key to return...");
                Console.ReadKey(true);
                return false;
            }

            var devices = await _spotify.Player.GetAvailableDevices();
            var active = devices.Devices.FirstOrDefault(d => d.IsActive);

            if (active == null)
            {
                if (devices.Devices.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nNo devices available. Open Spotify on a device (desktop/mobile/web) and try again.");
                    Console.ResetColor();
                    Console.WriteLine("Press any key to return...");
                    Console.ReadKey(true);
                    return false;
                }

                Console.WriteLine("\nNo active device. Select a device to activate:");
                for (int i = 0; i < devices.Devices.Count; i++)
                    Console.WriteLine($"{i + 1}. {devices.Devices[i].Name} ({devices.Devices[i].Type})");

                Console.Write("Enter number or press Enter to cancel: ");
                var sel = Console.ReadLine();
                if (!int.TryParse(sel, out var num) || num < 1 || num > devices.Devices.Count)
                    return false;

                var targetId = devices.Devices[num - 1].Id;
                await _spotify.Player.TransferPlayback(new PlayerTransferPlaybackRequest(new[] { targetId }) { Play = false });
            }

            return true;
        }

        private static async Task ShowCurrentSongAsync()
        {
            try
            {
                var playback = await _spotify.Player.GetCurrentPlayback();
                if (playback?.Item is FullTrack track)
                {
                    Console.WriteLine($"\nNow Playing: {track.Name} by {string.Join(", ", track.Artists.Select(a => a.Name))}");
                    Console.WriteLine($"Album: {track.Album.Name}");
                    Console.WriteLine($"Device: {playback.Device?.Name}");
                    Console.WriteLine($"Link: {track.ExternalUrls["spotify"]}");
                }
                else
                {
                    Console.WriteLine("\nNo song is currently playing.");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nError retrieving current song: {ex.Message}");
                Console.ResetColor();
            }
            Console.WriteLine("\nPress any key to return...");
            Console.ReadKey(true);
        }

        private static async Task TogglePlayPauseAsync()
        {
            var playback = await _spotify.Player.GetCurrentPlayback();
            if (playback?.IsPlaying == true)
                await _spotify.Player.PausePlayback();
            else
                await _spotify.Player.ResumePlayback();
            Console.WriteLine("\nToggled play/pause. Press any key to return...");
            Console.ReadKey(true);
        }

        private static async Task SearchAndPlaySongAsync()
        {
            Console.Write("\nEnter song name to search: ");
            var query = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine("No query entered.");
                Console.WriteLine("\nPress any key to return...");
                Console.ReadKey(true);
                return;
            }

            var search = await _spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, query));
            var tracks = search.Tracks?.Items?.Take(5).ToList() ?? Enumerable.Empty<FullTrack>().ToList();
            if (tracks.Count == 0)
            {
                Console.WriteLine("\nNo song found.");
                Console.WriteLine("\nPress any key to return...");
                Console.ReadKey(true);
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\nTop results:");
            Console.ResetColor();
            for (int i = 0; i < tracks.Count; i++)
                Console.WriteLine($"{i + 1}. {tracks[i].Name} — {string.Join(", ", tracks[i].Artists.Select(a => a.Name))}");

            Console.Write("\nChoose a track (1-{0}) or Enter to cancel: ", tracks.Count);
            var sel = Console.ReadLine();
            if (!int.TryParse(sel, out var idx) || idx < 1 || idx > tracks.Count) return;

            var track = tracks[idx - 1];
            await _spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest { Uris = new[] { track.Uri } });
            Console.WriteLine($"\nPlaying: {track.Name} by {string.Join(", ", track.Artists.Select(a => a.Name))}");
            Console.WriteLine("\nPress any key to return...");
            Console.ReadKey(true);
        }
    }
}
