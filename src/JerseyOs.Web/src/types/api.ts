/* Wrapper around generated OpenAPI types. Do not hand-edit api.generated.ts. */
import type { components } from '@/types/api.generated';

export type UserDto = components['schemas']['UserResponse'];
export type LoginRequestDto = components['schemas']['LoginRequest'];
export type HealthCheckDto = components['schemas']['HealthCheckResponse'];
export type SystemHealthDto = components['schemas']['SystemHealthResponse'];

export interface SessionDto {
  accessToken: components['schemas']['AuthResponse']['accessToken'];
  user: UserDto;
}

export interface ProblemDetailsDto {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
}
