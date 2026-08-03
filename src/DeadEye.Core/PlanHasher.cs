using System.Security.Cryptography;
using System.Text.Json;

namespace DeadEye.Core;

public static class PlanHasher
{
    public static string Compute(ActionPlan plan)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { plan.ActionId, plan.Version, plan.Module, plan.Actions, plan.UserSid, plan.SessionId });
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
