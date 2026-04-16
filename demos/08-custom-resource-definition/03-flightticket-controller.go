// =============================================================================
// Kubernetes Custom Controller for FlightTicket — Educational Reference
// =============================================================================
//
// What: A custom controller watches for Custom Resource (CR) events and
//   reconciles the actual state to match the desired state defined in the CR.
//   This is the "operator pattern" — a CRD defines the schema, and a
//   controller provides the behavior.
//
// How it works:
//   1. Controller registers a watch on FlightTicket resources via the API server
//   2. When a FlightTicket CR is created/updated/deleted, the API server sends an event
//   3. The controller's Reconcile() function is called with the CR's name/namespace
//   4. Reconcile fetches the CR, processes it, and optionally updates its status
//
//   API Server                     Controller
//       |                              |
//       |--- Watch FlightTickets ----->|
//       |                              |
//   CR created (kubectl apply)         |
//       |--- Event: CR added --------->|
//       |                              |-- Reconcile()
//       |                              |   - Fetch CR
//       |                              |   - Business logic (book ticket)
//       |                              |   - Update CR status
//       |<-- Status update ------------|
//
// Key concepts:
//   - Reconcile loop: Idempotent function called for every change; must handle
//     create, update, and delete scenarios
//   - Controller-runtime: The standard Go library for building controllers
//     (sigs.k8s.io/controller-runtime)
//   - NamespacedName: Identifies which CR triggered the reconciliation
//   - Status subresource: Separate API endpoint for updating .status without
//     modifying .spec (avoids conflicts with users)
//
// Prerequisites:
//   - Go 1.21+
//   - controller-runtime library: go get sigs.k8s.io/controller-runtime
//   - The CRD must be applied: kubectl apply -f 01-flightticket-crd.yaml
//
// See also:
//   - 01-flightticket-crd.yaml — defines the FlightTicket schema
//   - 02-flightticket-resource.yaml — example FlightTicket instances
// =============================================================================

package flightticket

import (
	"context"
	"fmt"

	metav1 "k8s.io/apimachinery/pkg/apis/meta/v1"
	"k8s.io/apimachinery/pkg/runtime"
	"k8s.io/apimachinery/pkg/runtime/schema"
	ctrl "sigs.k8s.io/controller-runtime"
	"sigs.k8s.io/controller-runtime/pkg/client"
	"sigs.k8s.io/controller-runtime/pkg/log"
)

// =============================================================================
// Custom Resource Types
// =============================================================================

// FlightTicket defines the structure of the FlightTicket custom resource.
// It embeds TypeMeta (apiVersion, kind) and ObjectMeta (name, namespace, labels).
type FlightTicket struct {
	metav1.TypeMeta   `json:",inline"`
	metav1.ObjectMeta `json:"metadata,omitempty"`
	Spec              FlightTicketSpec   `json:"spec,omitempty"`
	Status            FlightTicketStatus `json:"status,omitempty"`
}

// FlightTicketSpec defines the desired state — what the user specifies.
type FlightTicketSpec struct {
	From   string `json:"from"`   // Departure city
	To     string `json:"to"`     // Destination city
	Number int    `json:"number"` // Number of tickets (1-10)
}

// FlightTicketStatus defines the observed state — what the controller reports.
type FlightTicketStatus struct {
	Phase            string `json:"phase,omitempty"`            // Pending, Confirmed, Cancelled
	BookingReference string `json:"bookingReference,omitempty"` // Reference ID after confirmation
}

// DeepCopyObject implements runtime.Object interface.
// Required for any type registered with the Kubernetes API machinery.
func (in *FlightTicket) DeepCopyObject() runtime.Object {
	out := *in
	in.ObjectMeta.DeepCopyInto(&out.ObjectMeta)
	return &out
}

// FlightTicketList contains a list of FlightTicket resources.
// Required for the controller to list/watch resources.
type FlightTicketList struct {
	metav1.TypeMeta `json:",inline"`
	metav1.ListMeta `json:"metadata,omitempty"`
	Items           []FlightTicket `json:"items"`
}

// DeepCopyObject implements runtime.Object for the list type.
func (in *FlightTicketList) DeepCopyObject() runtime.Object {
	out := *in
	if in.Items != nil {
		out.Items = make([]FlightTicket, len(in.Items))
		copy(out.Items, in.Items)
	}
	return &out
}

// GroupVersion is the API group and version for FlightTicket.
var GroupVersion = schema.GroupVersion{Group: "flight.com", Version: "v1"}

// =============================================================================
// Controller (Reconciler)
// =============================================================================

// FlightTicketReconciler reconciles FlightTicket objects.
// It holds a reference to the Kubernetes client for reading/writing resources.
type FlightTicketReconciler struct {
	client.Client
}

// Reconcile is called whenever a FlightTicket CR is created, updated, or deleted.
// It is designed to be idempotent — calling it multiple times produces the same result.
//
// Workflow:
//  1. Fetch the FlightTicket CR by name/namespace
//  2. If not found, it was deleted — nothing to do
//  3. If found, process the booking and update status
func (r *FlightTicketReconciler) Reconcile(ctx context.Context, req ctrl.Request) (ctrl.Result, error) {
	log := log.FromContext(ctx)

	// Step 1: Fetch the FlightTicket CR
	var ticket FlightTicket
	if err := r.Get(ctx, req.NamespacedName, &ticket); err != nil {
		// If the CR was deleted, r.Get returns NotFound — ignore it
		return ctrl.Result{}, client.IgnoreNotFound(err)
	}

	log.Info("Reconciling flight ticket",
		"name", ticket.Name,
		"from", ticket.Spec.From,
		"to", ticket.Spec.To,
		"passengers", ticket.Spec.Number,
	)

	// Step 2: Business logic — process the booking
	// In a real controller, this would call an external booking API
	if ticket.Status.Phase == "" || ticket.Status.Phase == "Pending" {
		ticket.Status.Phase = "Confirmed"
		ticket.Status.BookingReference = fmt.Sprintf("FT-%s-%s", ticket.Namespace, ticket.Name)

		// Step 3: Update the status subresource
		// Using StatusClient.Update() writes only to /status, not /spec
		if err := r.Status().Update(ctx, &ticket); err != nil {
			log.Error(err, "Failed to update FlightTicket status")
			return ctrl.Result{}, err
		}

		log.Info("Flight ticket confirmed",
			"bookingReference", ticket.Status.BookingReference,
		)
	}

	return ctrl.Result{}, nil
}

// SetupWithManager registers the controller with the controller-runtime manager.
// The manager handles leader election, metrics, health checks, and the event loop.
func (r *FlightTicketReconciler) SetupWithManager(mgr ctrl.Manager) error {
	return ctrl.NewControllerManagedBy(mgr).
		For(&FlightTicket{}).  // Watch FlightTicket CRs
		Complete(r)            // Use this reconciler
}

// =============================================================================
// Usage
// =============================================================================
//
// This controller is typically started from a main.go entry point:
//
//   func main() {
//       mgr, _ := ctrl.NewManager(ctrl.GetConfigOrDie(), ctrl.Options{})
//       reconciler := &flightticket.FlightTicketReconciler{Client: mgr.GetClient()}
//       reconciler.SetupWithManager(mgr)
//       mgr.Start(ctrl.SetupSignalHandler())
//   }
//
// Build and run:
//   go build -o controller .
//   ./controller
//
// Or run locally against a cluster:
//   go run main.go
//
// The controller needs RBAC permissions on the FlightTicket resource.
// Example ClusterRole rules:
//   - apiGroups: ["flight.com"]
//     resources: ["flighttickets"]
//     verbs: ["get", "list", "watch", "create", "update", "patch", "delete"]
//   - apiGroups: ["flight.com"]
//     resources: ["flighttickets/status"]
//     verbs: ["get", "update", "patch"]
