/**
 * The paged envelope every collection endpoint returns (AP-3, docs/api-design.md §2.1).
 * Every list service in every later story returns this type — none re-invents it.
 */
export interface Paged<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalItems: number;
    totalPages: number;
}

/** Query parameters every collection endpoint accepts (docs/api-design.md §2.1). */
export interface PageRequest {
    /** 1-based, default 1. */
    page?: number;
    /** Default 25, max 100. */
    pageSize?: number;
    /** `field:direction`, restricted to a per-endpoint whitelist (AP-15). */
    sort?: string;
}
