import type { Role } from '../../types/domain';

/** Single source of truth for each role's landing page. */
export const ROLE_HOMES: Record<Role, string> = {
  Administrator: '/super-admin',
  ClinicManager: '/manager',
  Veterinarian: '/vet',
  InventoryOfficer: '/inventory-dashboard',
  PetOwner: '/pet-owner',
};

export const CLINICAL_ROLES: Role[] = ['PetOwner', 'Administrator', 'ClinicManager', 'Veterinarian'];
export const CLINICAL_STAFF_ROLES: Role[] = ['Administrator', 'Veterinarian'];
export const MANAGEMENT_ROLES: Role[] = ['Administrator', 'ClinicManager'];
export const INVENTORY_ROLES: Role[] = ['Administrator', 'ClinicManager', 'InventoryOfficer'];

/**
 * Prefix → allowed roles, mirroring the <RoleRoute> declarations in
 * AppRoutes. Used to decide whether a saved "return to" URL is safe to
 * honor for a given role after login. Paths match exactly or on a '/'
 * boundary, so '/inventory' does not match '/inventory-dashboard'.
 */
const ROUTE_ACCESS: ReadonlyArray<[prefix: string, roles: readonly Role[]]> = [
  ['/super-admin', ['Administrator']],
  ['/manager', MANAGEMENT_ROLES],
  ['/vet', ['Administrator', 'Veterinarian']],
  ['/inventory-dashboard', ['Administrator', 'InventoryOfficer']],
  ['/pet-owner', ['Administrator', 'PetOwner']],
  ['/dashboard', MANAGEMENT_ROLES],
  ['/pets', CLINICAL_ROLES],
  ['/consultations', CLINICAL_ROLES],
  ['/care-history', ['Administrator', 'PetOwner']],
  ['/prescriptions', ['PetOwner', 'Veterinarian', 'ClinicManager', 'Administrator']],
  ['/medical-history', ['Veterinarian', 'ClinicManager', 'Administrator']],
  ['/treatment', CLINICAL_STAFF_ROLES],
  ['/examinations', CLINICAL_STAFF_ROLES],
  ['/inventory', INVENTORY_ROLES],
  ['/scheduling', MANAGEMENT_ROLES],
  ['/billing', MANAGEMENT_ROLES],
  ['/approvals', MANAGEMENT_ROLES],
  ['/ai-workflows', MANAGEMENT_ROLES],
  ['/reports', MANAGEMENT_ROLES],
  ['/settings', ['Administrator']],
  ['/admin', ['Administrator']],
];

const SORTED_ROUTE_ACCESS = [...ROUTE_ACCESS].sort((a, b) => b[0].length - a[0].length);

export function homeForRole(role: Role | undefined): string {
  return role ? (ROLE_HOMES[role] ?? '/') : '/';
}

/**
 * Whether the role may open the given path. Unlisted paths return false —
 * callers should fall back to the role home rather than replay a URL the
 * role cannot see (which would flash the /unauthorized page).
 */
export function canRoleAccessPath(role: Role | undefined, path: string): boolean {
  if (!role) return false;
  const normalized = path.split('?')[0].split('#')[0] || '/';
  return SORTED_ROUTE_ACCESS.some(
    ([prefix, roles]) =>
      (normalized === prefix || normalized.startsWith(`${prefix}/`))
      && roles.includes(role),
  );
}

/**
 * Resolve a post-login redirect: honor the saved destination only when the
 * role can actually open it, otherwise land on the role's dashboard.
 */
export function safeRedirectPath(role: Role | undefined, from: string | undefined): string {
  return from && canRoleAccessPath(role, from) ? from : homeForRole(role);
}
