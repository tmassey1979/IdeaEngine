export interface DragonLogger {
  info(message: string, context?: Record<string, unknown>): void;
  error(message: string, context?: Record<string, unknown>): void;
}

export function createLogger(): DragonLogger {
  return {
    info(_message, _context) {},
    error(_message, _context) {}
  };
}