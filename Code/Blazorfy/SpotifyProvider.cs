using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Spotify.NetStandard.Client;
using Spotify.NetStandard.Client.Authentication;
using Spotify.NetStandard.Client.Interfaces;
using Spotify.NetStandard.Requests;
using Spotify.NetStandard.Responses;

namespace Blazorfy;

// Provider Class
public class SpotifyProvider
{
    // Constants
    private const string client_id = "clientid";
    private const int total = 50;
    private const int max = 100;

    // Members
    private readonly NavigationManager _navigation;
    private readonly ILocalStorageService _storage;
    private readonly ISpotifyApi _api;
    private readonly Uri _redirectUri;
    private AccessToken? _token;

    // Private Methods
    private Uri GetCurrentUri() =>
        _navigation.ToAbsoluteUri(_navigation.Uri);

    private async Task SetTokenAsync(AccessToken? token) =>
        await _storage.SetItemAsync(nameof(_token), token);

    // Constructor
    public SpotifyProvider(
    HttpClient client, 
    NavigationManager navigation, 
    ILocalStorageService storage)
    {
        _storage = storage;
        _navigation = navigation;
        _redirectUri = new Uri(GetCurrentUri().GetLeftPart(UriPartial.Path));
        _api = SpotifyClientFactory.CreateSpotifyClient(client, client_id).Api;
    }

    // Property
    public bool IsLoggedIn => 
        _token != null;

    // Login Method
    public async Task LoginAsync()
    {
        var responseUri = _api.GetAuthorisationCodeAuthUri(
            _redirectUri, 
            nameof(SpotifyProvider), 
            Scope.None, 
            out string codeVerifier);
        await _storage.SetItemAsync(nameof(codeVerifier), codeVerifier);
        if (responseUri != null)
            _navigation.NavigateTo(responseUri.ToString());
    }

    // Logout Method
    public async Task LogoutAsync()
    {
        await SetTokenAsync(_token = null);
        _navigation.NavigateTo(_redirectUri.ToString(), true);
    }

    // Is Logged In Method
    public async Task<bool> IsLoggedInAsync()
    {
        _token ??= await _storage.GetItemAsync<AccessToken>(nameof(_token));
        if (_token != null)
        {
            if (_token.Expiration < DateTime.UtcNow)
            {
                await LogoutAsync();
            }
            else
            {
                _api.Client.SetToken(_token);
            }
            return IsLoggedIn;
        }
        return false;
    }

    // Handle Code Method
    public async Task<bool> HandleCodeAsync(string? code)
    {
        if (code != null)
        {
            string codeVerifier = 
                await _storage.GetItemAsync<string>(nameof(codeVerifier));
            _token = await _api.GetAuthorisationCodeAuthTokenAsync(
                GetCurrentUri(), 
                _redirectUri, 
                nameof(SpotifyProvider), 
                codeVerifier);
            await SetTokenAsync(_token);
            _navigation.NavigateTo(_redirectUri.ToString());
        }
        return await IsLoggedInAsync();
    }

    // User Method
    public async Task<PrivateUser> GetUserAsync() =>
        await _api.GetUserProfileAsync();

    // List Method
    public async Task<List<TItem>> ListAsync<TItem>(string? id = null) 
    where TItem : class
    {
        var results = new List<TItem>();
        var page = new Page() { Limit = total };
        int count;
        do
        {
            Paging<TItem>? items = null;
            // Categories
            if (typeof(TItem) == typeof(Category))
            {
                items = await _api.GetAllCategoriesAsync(page: page) 
                as Paging<TItem>;
            }
            // Playlists
            if (typeof(TItem) == typeof(SimplifiedPlaylist))
            {
                items = await _api.GetCategoryPlaylistsAsync(id, page: page) 
                as Paging<TItem>;
            }
            // Albums

            if (items != null)
            {
                results.AddRange(items.Items);
                page.Offset += total;
            }
            count = items?.Count ?? 0;
        }
        while (count > 0 && results.Count < max && count == total);
        return results;
    }

    // Search Method
    public async Task<List<TItem>> SearchAsync<TItem>(string query) 
    where TItem : class
    {
        var results = new List<TItem>();
        var page = new Page() { Limit = total };
        int count;
        do
        {
            Paging<TItem>? items = null;
            var searchType = new SearchType()
            {
                Playlist = typeof(TItem) == typeof(SimplifiedPlaylist),
                Album = typeof(TItem) == typeof(Album),
                Show = typeof(TItem) == typeof(SimplifiedShow)
            };
            var content = await _api.SearchForItemAsync(query, searchType, page: page);
            // Playlists
            if (typeof(TItem) == typeof(SimplifiedPlaylist))
            {
                items = content.Playlists as Paging<TItem>;
            }
            // Albums

            // Podcasts

            if (items != null)
            {
                results.AddRange(items.Items);
                page.Offset += total;
            }
            count = items?.Count ?? 0;
        }
        while (count > 0 && results.Count < max && count == total);
        return results;
    }


}