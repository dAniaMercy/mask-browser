import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

export function middleware(request: NextRequest) {
  // CSRF protection - проверка Origin заголовка
  const origin = request.headers.get('origin');
  const host = request.headers.get('host');

  // Разрешенные origins (гарантируем string[], убираем undefined)
  const envApiUrl = process.env.NEXT_PUBLIC_API_URL ?? '';
  const allowedOrigins: string[] = [
    'http://109.172.101.73:5052',
    'https://109.172.101.73',
    'https://yourdomain.com', // замените на ваш домен
    envApiUrl,
  ].filter((o): o is string => o.trim().length > 0);

  // Для API запросов проверяем Origin
  if (request.nextUrl.pathname.startsWith('/api/proxy')) {
    const originHeader = request.headers.get('origin');
    const safeOrigin = originHeader ?? '';

    const isAllowed = allowedOrigins.some((allowed) =>
      safeOrigin.includes(allowed)
    );

    if (originHeader && !isAllowed) {
      return NextResponse.json(
        { error: 'Invalid origin' },
        { status: 403 }
      );
    }
  }

  // Security headers
  const response = NextResponse.next();

  response.headers.set('X-Frame-Options', 'SAMEORIGIN');
  response.headers.set('X-Content-Type-Options', 'nosniff');
  response.headers.set('X-XSS-Protection', '1; mode=block');
  response.headers.set('Referrer-Policy', 'strict-origin-when-cross-origin');
  response.headers.set(
    'Permissions-Policy',
    'geolocation=(), microphone=(), camera=()'
  );

  // HSTS (для HTTPS)
  if (request.nextUrl.protocol === 'https:') {
    response.headers.set(
      'Strict-Transport-Security',
      'max-age=31536000; includeSubDomains'
    );
  }

  return response;
}

export const config = {
  matcher: ['/api/:path*', '/((?!_next/static|_next/image|favicon.ico).*)'],
};
