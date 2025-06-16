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

4. **Test the Tasks Service**

   Send a POST request to the following endpoint to add a task:
   ```
   POST http://localhost:8000/tasks
   ```
   **Request Body:**
   ```json
   {
       "text": "a task",
       "title": "Do this, too"
   }
   ```

   Send a GET request to retrieve tasks:
   ```
   GET http://localhost:8000/tasks
   ```

5. **Access the Services**

   - **Auth Service**: Accessible internally within the Docker network.
   - **Users Service**: Accessible at `http://localhost:8080`.
   - **Tasks Service**: Accessible at `http://localhost:8000`.

6. **Stop the Services**

   To stop all running services, use:
   ```bash
   docker-compose down
   ```

7. **Deploy to Kubernetes**

   Navigate to the `kubernetes` directory:
   ```bash
   cd kubernetes
   ```

   Apply the deployment and service configurations for all modules:
   ```bash
   kubectl apply -f auth-deployment.yaml -f auth-service.yaml
   kubectl apply -f users-deployment.yaml -f users-service.yaml
   kubectl apply -f tasks-deployment.yaml -f tasks-service.yaml
   ```

   Access the services using Minikube:
   ```bash
   minikube service users-service
   minikube service tasks-service
   ```

   Example for testing the Users Service:
   ```
   POST http://<minikube-ip>:<users-service-port>/login
   ```
   **Request Body:**
   ```json
   {
     "email": "test@test.com",
     "password": "testers"
   }
   ```

   Example for testing the Tasks Service:
   ```
   POST http://<minikube-ip>:<tasks-service-port>/tasks
   ```
   **Request Body:**
   ```json
   {
       "text": "a task",
       "title": "Do this, too"
   }
   ```