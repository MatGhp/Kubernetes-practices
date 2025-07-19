package flightticket

import (
	"context"

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
var ControllerKind = schema.GroupVersion{Group: "flight.com", Version: "v1"}.WithKind("FlightTicket")

// FlightTicketReconciler reconciles a FlightTicket object
type FlightTicketReconciler struct {
	client.Client
}

// Reconcile processes FlightTicket resources
func (r *FlightTicketReconciler) Reconcile(ctx context.Context, req ctrl.Request) (ctrl.Result, error) {
	log := log.FromContext(ctx)

	var ticket FlightTicket
	if err := r.Get(ctx, req.NamespacedName, &ticket); err != nil {
		return ctrl.Result{}, client.IgnoreNotFound(err)
	}

	log.Info("Processing flight ticket", 
		"from", ticket.Spec.From, 
		"to", ticket.Spec.To,
		"passengers", ticket.Spec.Number)

	return ctrl.Result{}, nil
}
