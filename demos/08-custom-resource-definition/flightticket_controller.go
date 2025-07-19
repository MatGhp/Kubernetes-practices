package flightticket

import (
	"context"
	"fmt"

	metav1 "k8s.io/apimachinery/pkg/apis/meta/v1"
	"k8s.io/apimachinery/pkg/runtime/schema"
	ctrl "sigs.k8s.io/controller-runtime"
	"sigs.k8s.io/controller-runtime/pkg/client"
	"sigs.k8s.io/controller-runtime/pkg/log"
)

// FlightTicket defines the structure of our custom resource
type FlightTicket struct {
	metav1.TypeMeta   `json:",inline"`
	metav1.ObjectMeta `json:"metadata,omitempty"`
	Spec              FlightTicketSpec `json:"spec,omitempty"`
}

// FlightTicketSpec defines the desired state of FlightTicket
type FlightTicketSpec struct {
	From   string `json:"from"`
	To     string `json:"to"`
	Number int    `json:"number"`
}

// Define the GroupVersionKind for FlightTicket
var (
	GroupVersion = schema.GroupVersion{Group: "flight.com", Version: "v1"}
	
	// ControllerKind represents the kind of the FlightTicket resource
	ControllerKind = GroupVersion.WithKind("FlightTicket")
)

// FlightTicketReconciler reconciles a FlightTicket object
type FlightTicketReconciler struct {
	client.Client
}

// Reconcile implements the reconciliation logic for FlightTicket resources
func (r *FlightTicketReconciler) Reconcile(ctx context.Context, req ctrl.Request) (ctrl.Result, error) {
	logger := log.FromContext(ctx)

	// Fetch the FlightTicket instance
	var flightTicket FlightTicket
	if err := r.Get(ctx, req.NamespacedName, &flightTicket); err != nil {
		return ctrl.Result{}, client.IgnoreNotFound(err)
	}

	logger.Info("Processing FlightTicket", 
		"name", flightTicket.Name, 
		"from", flightTicket.Spec.From, 
		"to", flightTicket.Spec.To,
		"passengers", flightTicket.Spec.Number)

	return ctrl.Result{}, nil
}
