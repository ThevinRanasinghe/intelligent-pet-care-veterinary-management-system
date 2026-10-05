/// Shared async load state for all feature providers (idle → loading →
/// success | error). Centralised so pages consuming multiple providers
/// (e.g. the owner Appointments tab) don't hit ambiguous imports.
enum LoadState { idle, loading, success, error }
