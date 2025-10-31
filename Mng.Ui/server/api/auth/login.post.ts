export default defineEventHandler(async (event) => {
    
    try {
    
      const config = useRuntimeConfig()      
      const serverUrl = config.public.reactorUrl      

      const body = await readBody(event);

      const response = await fetch(serverUrl+'/api/v1/token/login', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(body)
      })

      if (response.ok) {      
        
        const data = await response.json()

        return data
      } 
      else 
      {       
        const data = await response.text()

        event.node.res.statusCode = response.status == 404 ? 404 : 400

        return data
      }
    } catch (error) {
       
        event.node.res.statusCode = 400

        return error
    }

  })