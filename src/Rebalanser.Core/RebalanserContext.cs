﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rebalanser.Core
{
    /// <summary>
    /// Creates a Rebalanser node that participates in a resource group
    /// </summary>
    public class RebalanserContext : IDisposable
    {
        private IRebalanserProvider rebalanserProvider;
        private CancellationTokenSource cts;

        public RebalanserContext()
        {
            this.rebalanserProvider = Providers.GetProvider();
        }

        /// <summary>
        /// Called when a rebalancing is triggered
        /// </summary>
        public event EventHandler OnCancelAssignment;

        /// <summary>
        /// Called once the node has been assigned new resources
        /// </summary>
        public event EventHandler OnAssignment;

        /// <summary>
        /// Called when a non recoverable error occurs
        /// </summary>
        public event EventHandler<OnErrorArgs> OnError;

        /// <summary>
        /// Starts the node
        /// </summary>
        /// <param name="resourceGroup">The id of the resource group</param>
        /// <returns></returns>
        public async Task StartAsync(string resourceGroup, ContextOptions contextOptions)
        {
            this.cts = new CancellationTokenSource();
            var onChangeActions = new OnChangeActions();
            onChangeActions.AddOnStartAction(StartActivity);
            onChangeActions.AddOnStopAction(CancelActivity);
            onChangeActions.AddOnErrorAction(RaiseError);
            await this.rebalanserProvider.StartAsync(resourceGroup, onChangeActions, this.cts.Token, contextOptions);
        }

        /// <summary>
        /// Returns the list of assigned resources. This is a blocking call that blocks until
        /// resources have been assigned. Note that if there are more nodes participating in 
        /// the resource group than there are resources, then the node may be assigned zero resources. Once
        /// rebalancing is complete, this method will return with an empty collection of resources. 
        /// </summary>
        /// <returns>A list of resources assigned to the node</returns>
        public IList<string> GetAssignedResources()
        {
            return this.rebalanserProvider.GetAssignedResources();
        }

        /// <summary>
        /// See GetAssignedResources(). To prevent unbounded blocking, this method receives a 
        /// CancellationToken which can be used to unblock the method
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public IList<string> GetAssignedResources(CancellationToken token)
        {
            return this.rebalanserProvider.GetAssignedResources(token);
        }

        private bool disposed;
        /// <summary>
        /// Initiates a shutdown of the node.
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                this.cts.Cancel(); // signals provider to stop
                var completionTask = Task.Run(async () => await this.rebalanserProvider.WaitForCompletionAsync());
                completionTask.Wait(5000); // waits for completion up to 5 seconds
                disposed = true;
            }
        }

        private void CancelActivity()
        {
            RaiseOnCancelAssignment(EventArgs.Empty);
        }

        protected virtual void RaiseOnCancelAssignment(EventArgs e)
        {
            OnCancelAssignment?.Invoke(this, e);
        }

        private void RaiseError(string message, bool autoRecoveryEnabled, Exception ex)
        {
            RaiseOnError(new OnErrorArgs(message, autoRecoveryEnabled, ex));
        }

        protected virtual void RaiseOnError(OnErrorArgs e)
        {
            OnError?.Invoke(this, e);
        }

        private void StartActivity()
        {
            RaiseOnAssignments(EventArgs.Empty);
        }

        protected virtual void RaiseOnAssignments(EventArgs e)
        {
            OnAssignment?.Invoke(this, e);
        }
    }
}
