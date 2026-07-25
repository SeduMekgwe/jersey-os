/* Generator boundary: replace only this module with generated OpenAPI DTO exports. */
export interface UserDto {
  id: string;
  displayName: string;
  email: string;
  permissions: string[];
}
export interface SessionDto {
  accessToken: string;
  user: UserDto;
}
export interface LoginRequestDto {
  email: string;
  password: string;
}
export interface HealthCheckDto {
  name: string;
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
  description?: string;
}
export interface SystemHealthDto {
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
  checkedAt: string;
  checks: HealthCheckDto[];
}
export interface ProblemDetailsDto {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
  [extension: string]: unknown;
}
