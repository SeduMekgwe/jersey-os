/* Wrapper around generated OpenAPI types. Do not hand-edit api.generated.ts. */
import type { components } from '@/types/api.generated';

export type UserDto = components['schemas']['UserResponse'];
export type LoginRequestDto = components['schemas']['LoginRequest'];
export type HealthCheckDto = components['schemas']['HealthCheckResponse'];
export type SystemHealthDto = components['schemas']['SystemHealthResponse'];
export type ProductDto = components['schemas']['ProductResponse'];
export type ProductSummaryDto = components['schemas']['ProductSummaryResponse'];
export type PagedProductsDto = components['schemas']['PagedProductsResponse'];
export type CreateProductDto = components['schemas']['CreateProductRequest'];
export type UpdateProductDto = components['schemas']['UpdateProductRequest'];
export type UpsertVariantDto = components['schemas']['UpsertVariantRequest'];
export type AdjustInventoryDto = components['schemas']['AdjustInventoryRequest'];
export type InventoryDto = components['schemas']['InventoryResponse'];
export type TaxonomyItemDto = components['schemas']['TaxonomyItemResponse'];
export type CreateTaxonomyItemDto = components['schemas']['CreateTaxonomyItemRequest'];

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
