// SE3110 TC-RE-008: a REJECTED fetch (network down, not an HTTP error
// response) must surface a user-visible error and settle cleanly.
import { render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SchedulingPage } from '../../features/scheduling/SchedulingPage';

describe('Network failure handling (SE3110 TC-RE-008)', () => {
  beforeEach(() => { vi.stubGlobal('fetch', vi.fn()); });
  afterEach(() => { vi.unstubAllGlobals(); });

  it('shows the error banner when fetch rejects (network down)', async () => {
    (fetch as unknown as ReturnType<typeof vi.fn>).mockRejectedValue(
      new TypeError('Failed to fetch'));

    render(<SchedulingPage />);

    await waitFor(() =>
      expect(screen.getByText('Failed to fetch')).toBeInTheDocument());
    // loading state must have resolved — the page is not stuck spinning
    expect(screen.queryByText('Loading schedule...')).not.toBeInTheDocument();
  });
});
