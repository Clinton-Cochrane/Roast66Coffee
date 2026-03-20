/**
 * Centralized app configuration. Override via environment variables.
 * @see roast66/.env.example for local development
 */
const DEFAULT_API_BASE_URL = 'https://roast66coffee.onrender.com/api';

export const API_BASE_URL =
  process.env.REACT_APP_API_URL || DEFAULT_API_BASE_URL;
