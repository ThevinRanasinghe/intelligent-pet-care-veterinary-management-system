namespace PetCare.Infrastructure.Seed;

/// <summary>
/// Fixed GUIDs for development/reference seed data so seeded rows are
/// reproducible across environments and migrations.
/// See docs/database/scheduling-billing-approval-domain-model.md#seed-data.
/// </summary>
public static class SeedIds
{
    public static readonly Guid VetAnikaPerera = new("11111111-0000-0000-0000-000000000001");
    public static readonly Guid VetRohanFernando = new("11111111-0000-0000-0000-000000000002");
    public static readonly Guid VetNadeeSilva = new("11111111-0000-0000-0000-000000000003");

    public static readonly Guid SlotAnikaPerera1 = new("22222222-0000-0000-0000-000000000001");
    public static readonly Guid SlotAnikaPerera2 = new("22222222-0000-0000-0000-000000000002");
    public static readonly Guid SlotRohanFernando1 = new("22222222-0000-0000-0000-000000000003");
    public static readonly Guid SlotNadeeSilva1 = new("22222222-0000-0000-0000-000000000004");

    // Dev/test-only fixtures (not applied via migration HasData; see DevelopmentSeeder).
    public static readonly Guid DevPetBuddy = new("33333333-0000-0000-0000-000000000001");
    public static readonly Guid DevPetMisty = new("33333333-0000-0000-0000-000000000002");

    public static readonly Guid DevAppointmentConfirmed = new("44444444-0000-0000-0000-000000000001");
    public static readonly Guid DevAppointmentReserved = new("44444444-0000-0000-0000-000000000002");

    public static readonly Guid DevQuotation = new("55555555-0000-0000-0000-000000000001");

    public static readonly Guid DevQuotationItemConsultation = new("66666666-0000-0000-0000-000000000001");
    public static readonly Guid DevQuotationItemTreatment = new("66666666-0000-0000-0000-000000000002");

    public static readonly Guid DevApprovalPending = new("77777777-0000-0000-0000-000000000001");

    public static readonly Guid DevClinicManagerUser = new("88888888-0000-0000-0000-000000000001");
}
