// Server-side proxy for MngScheduler API (system + user jobs)

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig();

  const odakHost = process.env.ODAK_HOST?.trim() || '192.168.20.20';
  const schedulerUrl =
    process.env.SERVER_SCHEDULER_URL ||
    process.env.SCHEDULER_URL ||
    config.serverSchedulerUrl ||
    config.public.schedulerUrl ||
    `http://${odakHost}:5090`;

  let path = getRouterParam(event, 'path') || '';
  const method = getMethod(event);

  if (path.startsWith('scheduler/')) {
    path = path.substring('scheduler/'.length);
  }
  if (path.startsWith('/')) {
    path = path.substring(1);
  }

  let body: unknown = null;
  if (method === 'POST' || method === 'PUT' || method === 'PATCH') {
    try {
      body = await readBody(event);
    } catch {
      // no body
    }
  }

  const query = getQuery(event);
  const queryString = new URLSearchParams(query as Record<string, string>).toString();
  const url = queryString
    ? `${schedulerUrl}/api/${path}?${queryString}`
    : `${schedulerUrl}/api/${path}`;

  try {
    return await $fetch(url, {
      method: method as 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE',
      ...(body != null && { body }),
      headers: {
        'Content-Type': 'application/json',
        ...(event.node.req.headers.authorization && {
          Authorization: event.node.req.headers.authorization as string,
        }),
      },
    });
  } catch (error: unknown) {
    const err = error as {
      status?: number;
      statusCode?: number;
      message?: string;
      data?: unknown;
    };
    throw createError({
      statusCode: err.status || err.statusCode || 500,
      statusMessage: err.message || 'Scheduler API request failed',
      data: err.data || err.message,
    });
  }
});
