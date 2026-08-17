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
export type PricingRuleDto = components['schemas']['PricingRuleResponse'];
export type CreatePricingRuleDto = components['schemas']['CreatePricingRuleRequest'];
export type PricePreviewDto = components['schemas']['PricePreviewResponse'];
export type CollectionDto = components['schemas']['CollectionResponse'];
export type CreateCollectionDto = components['schemas']['CreateCollectionRequest'];
export type SupplierDto = components['schemas']['SupplierResponse'];
export type CreateSupplierDto = components['schemas']['CreateSupplierRequest'];
export type SupplierScrapeRunDto = components['schemas']['SupplierScrapeRunResponse'];
export type ImportBatchSummaryDto = components['schemas']['ImportBatchSummaryResponse'];
export type ImportBatchDetailDto = components['schemas']['ImportBatchDetailResponse'];
export type ImportItemDto = components['schemas']['ImportItemResponse'];
export type SalesChannelDto = components['schemas']['SalesChannelResponse'];
export type PublishRunDto = components['schemas']['PublishRunResponse'];
export type WebhookDeliveryDto = components['schemas']['WebhookDeliveryResponse'];
export type AiPromptTemplateDto = components['schemas']['AiPromptTemplateResponse'];
export type AiGenerationDto = components['schemas']['AiGenerationResponse'];
export type CreateAiGenerationDto = components['schemas']['CreateAiGenerationRequest'];
export type AuditEventDto = components['schemas']['AuditEventResponse'];
export type NotificationMessageDto = components['schemas']['NotificationMessageResponse'];
export type NotificationTemplateDto = components['schemas']['NotificationTemplateResponse'];
export type ApiKeyDto = components['schemas']['ApiKeyResponse'];
export type CreatedApiKeyDto = components['schemas']['CreatedApiKeyResponse'];

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
