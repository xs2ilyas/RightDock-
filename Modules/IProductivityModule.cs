using System.Windows;

namespace RightDock.Modules
{
    internal interface IProductivityModule
    {
        string Id { get; }

        string Name { get; }

        string Description { get; }

        string IconKey { get; }

        bool ShowInDockByDefault { get; }

        void Initialize(ModuleContext context);

        FrameworkElement CreateCompactView();

        FrameworkElement CreateExpandedView();

        FrameworkElement CreateSettingsView();

        void Activate();

        void Deactivate();
    }

    internal abstract class ProductivityModule : IProductivityModule
    {
        public abstract string Id { get; }

        public abstract string Name { get; }

        public virtual string Description => string.Empty;

        public abstract string IconKey { get; }

        public virtual bool ShowInDockByDefault => true;

        public virtual void Initialize(ModuleContext context)
        {
        }

        public abstract FrameworkElement CreateCompactView();

        public virtual FrameworkElement CreateExpandedView()
        {
            return null;
        }

        public virtual FrameworkElement CreateSettingsView()
        {
            return null;
        }

        public virtual void Activate()
        {
        }

        public virtual void Deactivate()
        {
        }
    }
}
