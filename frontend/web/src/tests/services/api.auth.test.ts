import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { apiRequest, ApiError } from '../../services/api';
import { getStoredAuth, setStoredAuth } from '../../utils/authStorage';
import type { StoredAuth } from '../../utils/authStorage';

function jsonResponse(body: unknown, ok = true, status = 200) {
  return { ok, status, text: async () => (body === undefined ? '' : JSON.stringify(body)) } as Response;
}

const validAuth: StoredAuth = {
  token: 'the-token',
  expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  userId: 'u-1',
  email: 'test@petcare.lk',
  name: 'Test',
  role: 'ClinicManager',
};

describe('api auth handling', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    localStorage.clear();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it('attaches the Authorization header when a token exists', async () => {
    setStoredAuth(validAuth);
    fetchMock.mockResolvedValueOnce(jsonResponse({ ok: true }));

    await apiRequest<unknown>('/test');

    const [, init] = fetchMock.mock.calls[0];
    expect(init.headers).toMatchObject({ Authorization: 'Bearer the-token' });
  });

  it('does not attach an Authorization header when the user is not logged in', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ ok: true }));

    await apiRequest<unknown>('/test');

    const [, init] = fetchMock.mock.calls[0];
    expect(init.headers).toMatchObject({ 'Content-Type': 'application/json' });
    expect(init.headers).not.toHaveProperty('Authorization');
  });

  it('clears the stored session and throws on 401', async () => {
    setStoredAuth(validAuth);
    fetchMock.mockResolvedValueOnce(jsonResponse({ detail: 'Unauthorized' }, false, 401));

    await expect(apiRequest<unknown>('/test')).rejects.toBeInstanceOf(ApiError);

    expect(getStoredAuth()).toBeNull();
  });

  it('throws but keeps the stored session on 403', async () => {
    setStoredAuth(validAuth);
    fetchMock.mockResolvedValueOnce(jsonResponse({ detail: 'Forbidden' }, false, 403));

    let err: unknown;
    try {
      await apiRequest<unknown>('/test');
    } catch (e) {
      err = e;
    }

    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).status).toBe(403);
    expect(getStoredAuth()).toEqual(validAuth);
  });
});
