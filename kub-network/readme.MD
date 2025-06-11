### Running Instructions

1. **Build and Push Docker Images**

   Run the following command to build the Docker images for all services:
   ```bash
   docker-compose up -d --build
   ```

   If needed, push the images to your Docker registry:
   ```bash
   docker-compose push
   ```

2. **Run the Services**

   Use the following command to start all services defined in the `docker-compose.yaml` file:
   ```bash
   docker-compose up -d
   ```

3. **Test the Users Service**

   Send a POST request to the following endpoint to test the Users Service:
   ```
   POST http://localhost:8080/login
   ```
   **Request Body:**
   ```json
   {
     "email": "test@test.com",
     "password": "testers"
   }
   ```



   post http://localhost:8000/tasks
   ```json
    {
        "text": "a task",
        "title": "Do this, too"
    }
   ```

4. **Access the Services**

   - **Auth Service**: Accessible internally within the Docker network.
   - **Users Service**: Accessible at `http://localhost:8080`.
   - **Tasks Service**: Accessible at `http://localhost:8000`.

5. **Stop the Services**

   To stop all running services, use:
   ```bash
   docker-compose down
   ```