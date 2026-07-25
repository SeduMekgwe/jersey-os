import { QueryClient } from '@tanstack/react-query';
import { ApiError } from '@/lib/api';
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (count, error) =>
        !(error instanceof ApiError && error.response.status < 500) && count < 2,
      staleTime: 30_000,
    },
  },
});
