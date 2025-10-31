interface RequestOptions {
  method?: "GET" | "POST" | "PUT" | "DELETE";
  headers?: Record<string, string>;
  body?: any;
}

export function fetchData(url = "", method = "GET", body = "", headers = {}) {
  return new Promise(async (resolve, reject) => {
    try {
      const options = {
        method,        
        headers: {
          "Content-Type": "application/json",
          Authorization: "Bearer " + useCookie("access_token").value,
          ...headers,
        },
      };

      if (body != '') {
        options.body = body;
      }

      const response = await fetch(
        useRuntimeConfig().public.reactorUrl + url,
        options
      );

      if (!response.ok) {
        const resError = await response.text();
        console.log(resError);
        throw new Error(resError);
      }

      const data = await response.json();
      resolve(data);
    } catch (error) {
      reject(error);
    }
  });
}

export function fetchDataWithoutToken<T>(
  url = "",
  method = "GET",
  body = "",
  headers = {}
): Promise<T> {
  return new Promise(async (resolve, reject) => {
    try {
      const options = {
        method,
        body,
        headers: {
          "Content-Type": "application/json",
          ...headers,
        },
      };

      if (body) {
        options.body = body;
      }

      const response = await fetch(url, options);

      if (!response.ok) {
        const resError = await response.text();
        throw new Error(resError);
      }

      const data = await response.json();
      resolve(data);
    } catch (error) {
      reject(error);
    }
  });
}
