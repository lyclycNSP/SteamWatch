using SteamWatch.Core.Limits;

namespace SteamWatch.Core.Export;

public sealed record ExportLimitRule(
    LimitScope Scope,
    LimitPeriod Period,
    int MaxMinutes,
    EnforcementMode Enforcement,
    int? AppId = null,
    string Name = "");
