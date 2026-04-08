import { NextRequest, NextResponse } from "next/server";

export function proxy(request: NextRequest): NextResponse {
  if (process.env.NODE_ENV !== "development") {
    const authSub = request.headers.get("X-Auth-Sub");
    if (!authSub) {
      return NextResponse.json(
        {
          type: "https://tools.ietf.org/html/rfc9457",
          title: "Unauthorized",
          status: 401,
          detail: "Missing X-Auth-Sub header",
        },
        {
          status: 401,
          headers: { "Content-Type": "application/problem+json" },
        }
      );
    }
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
