using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using SnakeWebForms.Models;

namespace SnakeWebForms.Services
{
    public class GameSessionManager
    {
        private const string CookieName = "snake_sid";
        private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(60);

        private readonly IMemoryCache _cache;

        public GameSessionManager(IMemoryCache cache)
        {
            _cache = cache;
        }

        public SnakeGameEngine GetOrCreateEngine(HttpContext httpContext)
        {
            string sessionId = httpContext.Request.Cookies[CookieName];
            if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > 64)
            {
                sessionId = Guid.NewGuid().ToString("N");
                httpContext.Response.Cookies.Append(CookieName, sessionId, new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = httpContext.Request.IsHttps,
                    Expires = DateTimeOffset.UtcNow.AddDays(7)
                });
            }

            string cacheKey = "snake_engine:" + sessionId;
            return _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.SlidingExpiration = SessionLifetime;
                return new SnakeGameEngine();
            });
        }
    }
}
