using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Credfeto.Keys.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Keys.Server.Helpers;

public static class SelfTest
{
    private const string SELF_TEST_SWITCH = "--self-test";

    public static bool IsSelfTest(IReadOnlyList<string> args)
    {
        return args.Any(static arg => StringComparer.Ordinal.Equals(x: arg, y: SELF_TEST_SWITCH));
    }

    public static ValueTask<int> RunAsync()
    {
        return RunAsync(BuildPlaceholderConfiguration());
    }

    public static async ValueTask<int> RunAsync(IEnumerable<KeyValuePair<string, string?>> configurationOverrides)
    {
        try
        {
            ExerciseGlobalisation();
            ExerciseCryptography();

            await using (
                WebApplication app = ServerStartup.CreateApp(args: [], configurationOverrides: configurationOverrides)
            )
            {
                ExerciseServices(app.Services);
            }

            Console.WriteLine("Self-test passed");

            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine("Self-test failed:");
            Console.WriteLine(exception.Message);
            Console.WriteLine(exception.StackTrace);

            return 1;
        }
    }

    // The self-test runs during docker build, where no real secrets are available.
    private static IEnumerable<KeyValuePair<string, string?>> BuildPlaceholderConfiguration()
    {
        return [new(key: "Challenge:HmacSecret", value: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)))];
    }

    private static void ExerciseGlobalisation()
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(name: "en-GB", predefinedOnly: false);

        _ = DateTimeOffset.UnixEpoch.ToString(format: "D", formatProvider: culture);
    }

    private static void ExerciseCryptography()
    {
        _ = SHA256.HashData(Encoding.UTF8.GetBytes(SELF_TEST_SWITCH));
    }

    private static void ExerciseServices(IServiceProvider services)
    {
        IChallengeService challengeService = services.GetRequiredService<IChallengeService>();

        _ = challengeService.GenerateAddChallenge(host: "self-test.example.com", user: "self-test");
    }
}
