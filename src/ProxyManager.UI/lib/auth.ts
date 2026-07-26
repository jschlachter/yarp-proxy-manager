import type { UserSession } from "@/types";

export function getSession(headers: Headers): UserSession {
  let userId: string;
  let groups: string[];
  let name: string;
  let accessToken: string;

  if (
    process.env.NODE_ENV === "development" &&
    !headers.get("X-Auth-Sub") &&
    process.env.DEV_AUTH_SUB
  ) {
    userId = process.env.DEV_AUTH_SUB;
    name = "";
    groups = process.env.DEV_AUTH_GROUPS
      ? process.env.DEV_AUTH_GROUPS.split(",").map((g) => g.trim())
      : [];
    accessToken = process.env.DEV_AUTH_TOKEN ?? "";
  } else {
    console.log(headers.get("X-Auth-Sub"), headers.get("X-auth-groups"), headers.get("X-Auth-Name"));
    userId = headers.get("X-Auth-Sub") ?? "";
    name = headers.get("X-Auth-Name") ?? "";
    const rawGroups = headers.get("X-auth-groups") ?? "";

    groups = rawGroups
      ? rawGroups.split(",").map((g) => g.trim()).filter(Boolean)
      : [];

    const authHeader = headers.get("Authorization") ?? "";
    accessToken = authHeader.startsWith("Bearer ")
      ? authHeader.slice(7)
      : authHeader;
  }

  const adminGroupClaim = process.env.ADMIN_GROUP_CLAIM ?? "proxy-admins";
  const isAdmin = groups.includes(adminGroupClaim);

  return { userId, name, groups, isAdmin, accessToken };
}
