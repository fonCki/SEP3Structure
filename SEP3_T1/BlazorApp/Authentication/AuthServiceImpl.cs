using System.Security.Claims;
using System.Text.Json;
using Contracts.Services;
using Entities.Model;
using Microsoft.JSInterop;

namespace BlazorApp.Authentication; 

public class AuthServiceImpl : IAuthService {
    public Action<ClaimsPrincipal> OnAuthStateChanged { get; set; } = null!; // assigning to null! to suppress null warning.
    public User MyUser { get; set; } = null!;
    private readonly IUserService userService;
    private readonly IJSRuntime jsRuntime;

    public AuthServiceImpl(IUserService userService, IJSRuntime jsRuntime)
    {
        this.userService = userService;
        this.jsRuntime = jsRuntime;
    }

    public async Task LoginAsync(string email, string password) {
        
        MyUser = await userService.GetUserAsyncByEmail(email); // Get user from database


        ValidateLoginCredentials(password, MyUser); // Validate input data against data from database
        // validation success
        
        await CacheUserAsync(MyUser.Email); // Cache the Email //TO CHANGE FOR UNIQUE TOKEN

        MyUser.Status = await userService.SetStatus(MyUser.RUI, Status.Online); // Set as online
        
        ClaimsPrincipal principal = CreateClaimsPrincipal(MyUser); // convert user object to ClaimsPrincipal
        
        OnAuthStateChanged?.Invoke(principal); // notify interested classes in the change of authentication state
    }

    public async Task LogoutAsync()
    {
        await ClearUserFromCacheAsync(); // remove the user object from browser cache
        ClaimsPrincipal principal = CreateClaimsPrincipal(null); // create a new ClaimsPrincipal with nothing.
        await userService.SetStatus(MyUser.RUI, Status.Offline); // Set as online
        OnAuthStateChanged?.Invoke(principal); // notify about change in authentication state
        MyUser = null!;
        
    }

    public async Task<ClaimsPrincipal> GetAuthAsync() // this method is called by the authentication framework, whenever user credentials are reguired
    {
        MyUser =  await GetUserFromCacheAsync(); // retrieve cached user, if any

        ClaimsPrincipal principal = CreateClaimsPrincipal(MyUser); // create ClaimsPrincipal

        return principal;
    }

    private async Task<User?> GetUserFromCacheAsync()
    {
        string mailRunningUser = await jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "currentUser");
        if (string.IsNullOrEmpty(mailRunningUser)) return null;
        User user = await userService.GetUserAsyncByEmail(mailRunningUser);
        return user;
    }

    private static void ValidateLoginCredentials(string password, User? user)
    {
        if (user == null)
        {
            throw new Exception("Username not found");
        }

        if (!string.Equals(password,user.Password))
        {
            throw new Exception("Password incorrect");
        }
    }

    private static ClaimsPrincipal CreateClaimsPrincipal(User? user)
    {
        if (user != null)
        {
            ClaimsIdentity identity = ConvertUserToClaimsIdentity(user);
            return new ClaimsPrincipal(identity);
        }

        return new ClaimsPrincipal();
    }

    private async Task CacheUserAsync(string email) //GET THE TOKEN
    {
        await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "currentUser", email);
    }

    private async Task ClearUserFromCacheAsync()
    {
        await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "currentUser", "");
    }

    private static ClaimsIdentity ConvertUserToClaimsIdentity(User user)
    {
        // here we take the information of the User object and convert to Claims
        // this is (probably) the only method, which needs modifying for your own user type
        List<Claim> claims = new()
        {
            new Claim(ClaimTypes.Name, user.Name),
            new Claim("lastName", user.LastName),
             new Claim("email", user.Email),
             new Claim("pwd", user.Password),
             new Claim("avatar", user.Avatar)
        };

        return new ClaimsIdentity(claims, "apiauth_type");
    }
}