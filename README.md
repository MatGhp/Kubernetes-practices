docker build -t mojtabaghp/kub-data-demo:2 .

docker push mojtabaghp/kub-data-demo:2


kubectl apply -f environment.yaml -f host-pv.yaml -f host-pvc.yaml -f deployment.yaml -f service.yaml