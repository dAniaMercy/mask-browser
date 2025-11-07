import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

export function middleware(request: NextRequest) {
  // CSRF protection - проверка Origin заголовка
  const origin = request.headers.get('origin');
  const host = request.headers.get('host');

  // Разрешенные origins
  const allowedOrigins = [
    'http://109.172.101.73:5052',
    'https://109.172.101.73',
    'https://yourdomain.com', // Замените на ваш домен
    process.env.NEXT_PUBLIC_API_URL,
  ];

  // Для API запросов проверяем Origin
  if (request.nextUrl.pathname.startsWith('/api/proxy')) {
    if (origin && !allowedOrigins.some(allowed => (origin ?? '').includes(allowed))) {
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
  matcher: [
    '/api/:path*',
    '/((?!_next/static|_next/image|favicon.ico).*)',
  ],
};

