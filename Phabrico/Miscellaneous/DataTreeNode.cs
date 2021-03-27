using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Phabrico.Miscellaneous
{
    /// <summary>
    /// Represents a tree structure
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DataTreeNode<T> : IEnumerable<T>
    {
        private int depth;

        /// <summary>
        /// Maximum depth of the tree
        /// </summary>
        public int MaximumDepth { get; private set; }

        /// <summary>
        /// Current tree item
        /// </summary>
        public T Me { get; private set; }

        /// <summary>
        /// Parent of current tree item
        /// </summary>
        public DataTreeNode<T> Parent { get; set; }

        /// <summary>
        /// Children of current tree item
        /// </summary>
        public List<DataTreeNode<T>> Children { get; set; }

        /// <summary>
        /// Depth of current tree item
        /// </summary>
        public int Depth
        {
            get
            {
                return depth;
            }

            set
            {
                depth = value;

                foreach (DataTreeNode<T> child in Children)
                {
                    child.Depth = depth + 1;
                }
            }
        }

        /// <summary>
        /// Tree items which have the same parent as the current tree item
        /// </summary>
        public IEnumerable<DataTreeNode<T>> Siblings
        {
            get
            {
                if (Parent == null)
                    return new DataTreeNode<T>[0];
                else
                    return Parent.Children.Where(child => child != this).ToArray();
            }
        }

        /// <summary>
        /// Initializes a new tree item
        /// </summary>
        /// <param name="me"></param>
        public DataTreeNode(T me = default(T))
        {
            this.MaximumDepth = 0;
            this.Me = me;

            this.Children = new List<DataTreeNode<T>>();
        }
        
        /// <summary>
        /// Initializes a new tree item
        /// </summary>
        /// <param name="maximumDepth"></param>
        public DataTreeNode(int maximumDepth)
        {
            this.MaximumDepth = maximumDepth;
            this.Me = default(T);

            this.Children = new List<DataTreeNode<T>>();
        }
        
        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns></returns>
        public System.Collections.Generic.IEnumerator<T> GetEnumerator()
        {
            foreach (DataTreeNode<T> child in Children)
            {
                yield return child.Me;
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns></returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <summary>
        /// Adds new child tree item to the current tree item
        /// </summary>
        /// <param name="child"></param>
        public void Add(DataTreeNode<T> child)
        {
            child.MaximumDepth = MaximumDepth;
            child.Parent = this;
            if (Depth >= MaximumDepth)
            {
                child.Depth = child.MaximumDepth;
            }
            else
            {
                child.Depth = Depth + 1;
            }

            Children.Add(child);
        }

        /// <summary>
        /// Moves the current tree item to another parent
        /// </summary>
        /// <param name="newParent"></param>
        public void MoveTo(DataTreeNode<T> newParent)
        {
            Parent.Children.Remove(this);
            newParent.Children.Add(this);
            Parent = newParent;
        }

        /// <summary>
        /// Returns a readable description for the current tree item
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Me.ToString();
        }
    }
}
