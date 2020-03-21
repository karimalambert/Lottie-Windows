// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Expr = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools
{
    /// <summary>
    /// Optimizes a <see cref="Visual"/> tree by combining and removing containers and
    /// removing containers that are empty.
    /// </summary>
    sealed class GraphCompactor
    {
        bool _madeProgress;

        GraphCompactor()
        {
        }

        internal static Visual OptimizeContainers(Visual root)
        {
            // Running the optimization multiple times can improve the results.
            // Keep iterating as long as we are making progress.
            var compactor = new GraphCompactor();
            do
            {
                compactor._madeProgress = false;

                var graph = ObjectGraph<Node>.FromCompositionObject(root, includeVertices: true);

                compactor.Optimize(graph);
            } while (compactor._madeProgress);

            return root;
        }

        void GraphHasChanged() => _madeProgress = true;

        void Optimize(ObjectGraph<Node> graph)
        {
            // Discover the parents of each container
            foreach (var node in graph.CompositionObjectNodes)
            {
                switch (node.Object.Type)
                {
                    case CompositionObjectType.CompositionContainerShape:
                    case CompositionObjectType.ShapeVisual:
                        foreach (var child in ((IContainShapes)node.Object).Shapes)
                        {
                            graph[child].Parent = node.Object;
                        }

                        break;
                    case CompositionObjectType.ContainerVisual:
                        foreach (var child in ((ContainerVisual)node.Object).Children)
                        {
                            graph[child].Parent = node.Object;
                        }

                        break;
                }
            }

            SimplifyProperties(graph);
            OptimizeShapes(graph);
            OptimizeVisuals(graph);
        }

        void OptimizeVisuals(ObjectGraph<Node> graph)
        {
            PushPropertiesDownToShapeVisual(graph);
            CoalesceContainerVisuals(graph);
            CoalesceOrthogonalVisuals(graph);
            CoalesceOrthogonalContainerVisuals(graph);
            RemoveRedundantInsetClipVisuals(graph);
        }

        void OptimizeShapes(ObjectGraph<Node> graph)
        {
            ElideTransparentSpriteShapes(graph);
            OptimizeContainerShapes(graph);
            PushShapeTreeVisibilityIntoVisualTree(graph);
        }

        void OptimizeContainerShapes(ObjectGraph<Node> graph)
        {
            var containerShapes =
                (from pair in graph.CompositionObjectNodes
                 where pair.Object.Type == CompositionObjectType.CompositionContainerShape
                 let parent = (IContainShapes)pair.Node.Parent
                 select (node: pair.Node, container: (CompositionContainerShape)pair.Object, parent)).ToArray();

            CoalesceSiblingContainerShapes(graph);
            ElideEmptyContainerShapes(graph, containerShapes);
            ElideStructuralContainerShapes(graph, containerShapes);
            PushContainerShapeTransformsDown(graph, containerShapes);
            CoalesceContainerShapes2(graph, containerShapes);
            PushPropertiesDownToSpriteShape(graph, containerShapes);
            PushShapeVisbilityDown(graph, containerShapes);
        }

        // Finds sibling shape containers that has the same properties and combines them.
        static void CoalesceSiblingContainerShapes(ObjectGraph<Node> graph)
        {
            // Find the IContainShapes that have 1 or more children.
            var containersWith1OrMoreChildren = graph.CompositionObjectNodes.Where(n =>
                n.Object is IContainShapes shapeContainer &&
                shapeContainer.Shapes.Count > 1
            ).ToArray();

            foreach (var ch in containersWith1OrMoreChildren)
            {
                var container = (IContainShapes)ch.Object;
                var grouped = GroupSimilarChildContainers(container).ToArray();

                if (grouped.Any(g => g.Length > 1))
                {
                    // There was some grouping. Clear out the children and replace them.
                    container.Shapes.Clear();
                    foreach (var group in grouped)
                    {
                        // Add the first item from the group.
                        container.Shapes.Add(group[0]);
                        graph[group[0]].Parent = (CompositionObject)container;

                        if (group.Length > 1)
                        {
                            // If there is more than 1 item in the group then they are all containers
                            // and they are all equivalent.
                            // Add the contents of the other containers into the first container.
                            var first = (CompositionContainerShape)group[0];

                            // All of the items in the group will share the first container.
                            for (var i = 1; i < group.Length; i++)
                            {
                                // Move the children of each of the other containers into this container.
                                var groupI = (CompositionContainerShape)group[i];

                                foreach (var shape in groupI.Shapes)
                                {
                                    first.Shapes.Add(shape);
                                    graph[shape].Parent = first;
                                }

                                groupI.Shapes.Clear();
                            }
                        }
                    }
                }
            }
        }

        static IEnumerable<CompositionShape[]> GroupSimilarChildContainers(IContainShapes container)
        {
            var grouped = new List<CompositionContainerShape>();

            foreach (var child in container.Shapes)
            {
                if (!(child is CompositionContainerShape childContainer))
                {
                    if (grouped.Count > 0)
                    {
                        // Output the group so far.
                        yield return grouped.ToArray();
                        grouped.Clear();
                    }

                    // Output a group with only one item - the shape that is not a container.
                    yield return new[] { child };
                }
                else
                {
                    // The shape is a container.
                    if (grouped.Count == 0)
                    {
                        // Start a new group.
                        grouped.Add(childContainer);
                    }
                    else
                    {
                        // See if it belongs in the current group. It does if it is the same as
                        // the first item in the group except for having different children.
                        if (IsEquivalentContainer(grouped[0], childContainer))
                        {
                            grouped.Add(childContainer);
                        }
                        else
                        {
                            yield return grouped.ToArray();
                            grouped.Clear();
                            grouped.Add(childContainer);
                        }
                    }
                }
            }

            if (grouped.Count > 0)
            {
                // Output the final group.
                yield return grouped.ToArray();
            }
        }

        static bool IsEquivalentContainer(CompositionContainerShape a, CompositionContainerShape b)
        {
            if (a.Animators.Count != b.Animators.Count)
            {
                return false;
            }

            if (a.TransformMatrix != b.TransformMatrix ||
                a.CenterPoint != b.CenterPoint ||
                a.Offset != b.Offset ||
                a.RotationAngleInDegrees != b.RotationAngleInDegrees ||
                a.Scale != b.Scale ||
                a.Properties.Names.Count > 0 || b.Properties.Names.Count > 0)
            {
                return false;
            }

            if (a.Animators.Count > 0)
            {
                // There are some animations. Compare them.
                for (var i = 0; i < a.Animators.Count; i++)
                {
                    var aAnimator = a.Animators[i];
                    var bAnimator = b.Animators[i];

                    if (!AreAnimatorsEqual((a, aAnimator), (b, bAnimator)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        static bool AreAnimatorsEqual(
            (CompositionObject owner, CompositionObject.Animator animator) a,
            (CompositionObject owner, CompositionObject.Animator animator) b)
        {
            if (a.animator.AnimatedProperty != b.animator.AnimatedProperty)
            {
                return false;
            }

            if (a.animator.AnimatedObject != a.owner || b.animator.AnimatedObject != b.owner)
            {
                // We only handle the case of the animated object being the owner.
                return false;
            }

            if (a.animator.Animation.Type != b.animator.Animation.Type)
            {
                return false;
            }

            switch (a.animator.Animation.Type)
            {
                case CompositionObjectType.ExpressionAnimation:
                    {
                        var aAnimation = (ExpressionAnimation)a.animator.Animation;
                        var bAnimation = (ExpressionAnimation)b.animator.Animation;
                        if (aAnimation.Expression != bAnimation.Expression)
                        {
                            return false;
                        }

                        var aRefs = aAnimation.ReferenceParameters.ToArray();
                        var bRefs = bAnimation.ReferenceParameters.ToArray();

                        if (aRefs.Length != bRefs.Length)
                        {
                            return false;
                        }

                        for (var i = 0; i < aRefs.Length; i++)
                        {
                            var aRef = aRefs[i].Value;
                            var bRef = bRefs[i].Value;
                            if (aRef == bRef || (aRef == a.owner && bRef == b.owner))
                            {
                                return true;
                            }
                        }
                    }

                    break;

                case CompositionObjectType.ScalarKeyFrameAnimation:
                    {
                        var aAnimation = (ScalarKeyFrameAnimation)a.animator.Animation;
                        var bAnimation = (ScalarKeyFrameAnimation)b.animator.Animation;
                        if (aAnimation == bAnimation)
                        {
                            return true;
                        }
                    }

                    break;

                case CompositionObjectType.Vector2KeyFrameAnimation:
                    {
                        var aAnimation = (Vector2KeyFrameAnimation)a.animator.Animation;
                        var bAnimation = (Vector2KeyFrameAnimation)b.animator.Animation;
                        if (aAnimation == bAnimation)
                        {
                            return true;
                        }
                    }

                    break;

                // For now we only handle some types of animations.
                case CompositionObjectType.BooleanKeyFrameAnimation:
                case CompositionObjectType.ColorKeyFrameAnimation:
                case CompositionObjectType.PathKeyFrameAnimation:
                case CompositionObjectType.Vector3KeyFrameAnimation:
                case CompositionObjectType.Vector4KeyFrameAnimation:
                    return false;

                default:
                    throw new InvalidOperationException();
            }

            // TODO - if it's an expression animation, the reference parameters have to be
            //         pointing to the same object, or they must be pointing to the owner object.
            return false;
        }

        // Finds ContainerVisual with a single ShapeVisual child where the ContainerVisual
        // only exists to set an InsetClip. In this case the ContainerVisual can be removed
        // because the ShapeVisual has an implicit InsetClip.
        void RemoveRedundantInsetClipVisuals(ObjectGraph<Node> graph)
        {
            var containersClippingShapeVisuals = graph.CompositionObjectNodes.Where(n =>

                    // Find the ContainerVisuals that have only a Clip and Size set and have one
                    // child that is a ShapeVisual.
                    n.Object is ContainerVisual container &&
                    container.CenterPoint is null &&
                    container.Clip != null &&
                    container.Clip.Type == CompositionObjectType.InsetClip &&
                    container.IsVisible is null &&
                    container.Offset is null &&
                    container.Opacity is null &&
                    container.RotationAngleInDegrees is null &&
                    container.RotationAxis is null &&
                    container.Scale is null &&
                    container.Size != null &&
                    container.TransformMatrix is null &&
                    container.Animators.Count == 0 &&
                    container.Properties.Names.Count == 0 &&
                    container.Children.Count == 1 &&
                    container.Children[0].Type == CompositionObjectType.ShapeVisual
            ).ToArray();

            foreach (var (node, obj) in containersClippingShapeVisuals)
            {
                var container = (ContainerVisual)obj;
                var shapeVisual = (ShapeVisual)container.Children[0];

                // Check that the clip and size on the container is the same
                // as the size on the shape.
                var clip = (InsetClip)container.Clip;
                if ((!IsNullOrZero(clip.TopInset)) ||
                    (!IsNullOrZero(clip.RightInset)) ||
                    (!IsNullOrZero(clip.LeftInset)) ||
                    (!IsNullOrZero(clip.BottomInset)))
                {
                    continue;
                }

                if (!IsNullOrOne(clip.Scale))
                {
                    continue;
                }

                if (!IsNullOrZero(clip.CenterPoint))
                {
                    continue;
                }

                if (container.Size != shapeVisual.Size)
                {
                    continue;
                }

                // The container is redundant.
                var parent = node.Parent;
                if (parent is ContainerVisual parentContainer)
                {
                    GraphHasChanged();

                    // Replace the container with the ShapeVisual.
                    var indexOfRedundantContainer = parentContainer.Children.IndexOf(container);

                    // The container may have been already removed (this can happen if one of the
                    // coalescing methods here doesn't update the graph).
                    if (indexOfRedundantContainer >= 0)
                    {
                        parentContainer.Children.RemoveAt(indexOfRedundantContainer);
                        parentContainer.Children.Insert(indexOfRedundantContainer, shapeVisual);

                        CopyDescriptions(container, shapeVisual);
                    }
                }
            }
        }

        // Where possible, replace properties with a TransformMatrix.
        void SimplifyProperties(ObjectGraph<Node> graph)
        {
            foreach (var (_, obj) in graph.CompositionObjectNodes)
            {
                switch (obj.Type)
                {
                    case CompositionObjectType.ContainerVisual:
                    case CompositionObjectType.ShapeVisual:
                        SimplifyProperties((Visual)obj);
                        break;
                    case CompositionObjectType.CompositionContainerShape:
                    case CompositionObjectType.CompositionSpriteShape:
                        SimplifyProperties((CompositionShape)obj);
                        break;
                }
            }
        }

        // Remove the CenterPoint and RotationAxis properties if they're redundant,
        // and convert properties to TransformMatrix if possible.
        void SimplifyProperties(Visual obj)
        {
            var nonDefaultProperties = GetNonDefaultProperties(obj);
            if (obj.CenterPoint.HasValue &&
                ((nonDefaultProperties & (PropertyId.RotationAngleInDegrees | PropertyId.Scale)) == PropertyId.None))
            {
                GraphHasChanged();

                // Centerpoint and RotationAxis is not needed if Scale or Rotation are not used.
                obj.CenterPoint = null;
                obj.RotationAxis = null;
            }

            // Convert the properties to a transform matrix. This can reduce the
            // number of calls needed to initialize the object, and makes finding
            // and removing redundant containers easier.

            // We currently only support rotation around the Z axis here. Check for that.
            var hasNonStandardRotation =
                obj.RotationAngleInDegrees.HasValue && obj.RotationAngleInDegrees.Value != 0 &&
                obj.RotationAxis.HasValue && obj.RotationAxis != Vector3.UnitZ;

            if (obj.Animators.Count == 0 && !hasNonStandardRotation)
            {
                // Get the values of the properties, and the defaults for properties that are not set.
                var centerPoint = obj.CenterPoint ?? Vector3.Zero;
                var scale = obj.Scale ?? Vector3.One;
                var rotation = obj.RotationAngleInDegrees ?? 0;
                var offset = obj.Offset ?? Vector3.Zero;
                var transform = obj.TransformMatrix ?? Matrix4x4.Identity;

                // Clear out the properties.
                obj.CenterPoint = null;
                obj.Scale = null;
                obj.RotationAngleInDegrees = null;
                obj.Offset = null;
                obj.TransformMatrix = null;

                // Calculate the matrix that is equivalent to the properties.
                var combinedMatrix =
                    Matrix4x4.CreateScale(scale, centerPoint) *
                    Matrix4x4.CreateRotationZ(DegreesToRadians(rotation), centerPoint) *
                    Matrix4x4.CreateTranslation(offset) *
                    transform;

                // If the matrix actually does something, set it.
                if (!combinedMatrix.IsIdentity)
                {
                    if (combinedMatrix != transform)
                    {
                        GraphHasChanged();
                        var transformDescription = DescribeTransform(scale, rotation, offset);
                        AppendShortDescription(obj, transformDescription);
                        AppendLongDescription(obj, transformDescription);
                    }

                    obj.TransformMatrix = combinedMatrix;
                }
            }
        }

        // Remove the centerpoint property if it's redundant, and convert properties to TransformMatrix if possible.
        void SimplifyProperties(CompositionShape obj)
        {
            // Remove the centerpoint if it's not used by Scale or Rotation.
            var nonDefaultProperties = GetNonDefaultProperties(obj);
            if (obj.CenterPoint.HasValue &&
                ((nonDefaultProperties & (PropertyId.RotationAngleInDegrees | PropertyId.Scale)) == PropertyId.None))
            {
                GraphHasChanged();

                // Centerpoint is not needed if Scale or Rotation are not used.
                obj.CenterPoint = null;
            }

            // Convert the properties to a transform matrix. This can reduce the
            // number of calls needed to initialize the object, and makes finding
            // and removing redundant containers easier.
            if (obj.Animators.Count == 0)
            {
                // Get the values for the properties, and the defaults for the properties that are not set.
                var centerPoint = obj.CenterPoint ?? Vector2.Zero;
                var scale = obj.Scale ?? Vector2.One;
                var rotation = obj.RotationAngleInDegrees ?? 0;
                var offset = obj.Offset ?? Vector2.Zero;
                var transform = obj.TransformMatrix ?? Matrix3x2.Identity;

                // Clear out the properties.
                obj.CenterPoint = null;
                obj.Scale = null;
                obj.RotationAngleInDegrees = null;
                obj.Offset = null;
                obj.TransformMatrix = null;

                // Calculate the matrix that is equivalent to the properties.
                var combinedMatrix =
                    Matrix3x2.CreateScale(scale, centerPoint) *
                    Matrix3x2.CreateRotation(DegreesToRadians(rotation), centerPoint) *
                    Matrix3x2.CreateTranslation(offset) *
                    transform;

                // If the matrix actually does something, set it.
                if (!combinedMatrix.IsIdentity)
                {
                    if (combinedMatrix != transform)
                    {
                        GraphHasChanged();
                        var transformDescription = DescribeTransform(scale, rotation, offset);
                        AppendShortDescription(obj, transformDescription);
                        AppendLongDescription(obj, transformDescription);
                    }

                    obj.TransformMatrix = combinedMatrix;
                }
            }
        }

        static float DegreesToRadians(float angle) => (float)(Math.PI * angle / 180.0);

        static bool IsBrushTransparent(CompositionBrush brush)
        {
            return brush == null || (!brush.Animators.Any() && (brush as CompositionColorBrush)?.Color?.A == 0);
        }

        void ElideTransparentSpriteShapes(ObjectGraph<Node> graph)
        {
            var transparentShapes =
                (from pair in graph.CompositionObjectNodes
                 where pair.Object.Type == CompositionObjectType.CompositionSpriteShape
                 let shape = (CompositionSpriteShape)pair.Object
                 where IsBrushTransparent(shape.FillBrush) && IsBrushTransparent(shape.StrokeBrush)
                 select (Shape: shape, Parent: (IContainShapes)pair.Node.Parent)).ToArray();

            foreach (var pair in transparentShapes)
            {
                GraphHasChanged();

                pair.Parent.Shapes.Remove(pair.Shape);
            }
        }

        // Removes any CompositionContainerShapes that have no children.
        void ElideEmptyContainerShapes(
            ObjectGraph<Node> graph,
            (Node node, CompositionContainerShape container, IContainShapes parent)[] containerShapes)
        {
            // Keep track of which containers were removed so we don't consider them again.
            var removed = new HashSet<CompositionContainerShape>();

            // Keep going as long as progress is made.
            for (var madeProgress = true; madeProgress;)
            {
                madeProgress = false;
                foreach (var (_, container, parent) in containerShapes)
                {
                    if (!removed.Contains(container) && container.Shapes.Count == 0)
                    {
                        GraphHasChanged();

                        // Indicate that we successfully removed a container.
                        madeProgress = true;

                        // Remove the empty container.
                        parent.Shapes.Remove(container);

                        // Don't look at the removed object again.
                        removed.Add(container);
                    }
                }
            }
        }

        void PushContainerShapeTransformsDown(
            ObjectGraph<Node> graph,
            (Node node, CompositionContainerShape container, IContainShapes parent)[] containerShapes)
        {
            // If a container is not animated and has no other properties set apart from a transform,
            // and all of its children do not have an animated transform, the transform can be pushed down to
            // each child, and the container can be removed.
            // Note that this is safe because TransformMatrix effectively sits above all transforming
            // properties, so after pushing it down it will still be above all transforming properties.
            var elidableContainers = containerShapes.Where(n =>
            {
                var container = n.container;
                var containerProperties = GetNonDefaultProperties(container);

                if (container.Shapes.Count == 0)
                {
                    // Ignore empty containers.
                    return false;
                }

                if (container.Animators.Count != 0 || (containerProperties & ~PropertyId.TransformMatrix) != PropertyId.None)
                {
                    // Ignore this container if it has animators or anything other than the transform is set.
                    return false;
                }

                foreach (var child in container.Shapes)
                {
                    var childProperties = GetNonDefaultProperties(child);

                    if (child.Animators.Where(a => a.AnimatedProperty == "TransformMatrix").Any())
                    {
                        // Ignore this container if any of the children has an animated transform.
                        return false;
                    }
                }

                return true;
            });

            // Push the transform down to the child.
            foreach (var (_, container, _) in elidableContainers)
            {
                foreach (var child in container.Shapes)
                {
                    // Push the transform down to the child
                    if (container.TransformMatrix.HasValue)
                    {
                        child.TransformMatrix = (child.TransformMatrix ?? Matrix3x2.Identity) * container.TransformMatrix;
                    }
                }

                // Remove the container.
                ElideContainerShape(graph, container);
            }
        }

        void ElideStructuralContainerShapes(
            ObjectGraph<Node> graph,
            (Node node, CompositionContainerShape container, IContainShapes parent)[] containerShapes)
        {
            // If a container is not animated and has no properties set, its children can be inserted into its parent.
            var containersWithNoPropertiesSet = containerShapes.Where(n =>
            {
                var container = n.container;
                var containerProperties = GetNonDefaultProperties(container);

                if (container.Animators.Count != 0 || containerProperties != PropertyId.None)
                {
                    return false;
                }

                // Container has no properties set.
                return true;
            }).ToArray();

            foreach (var (_, container, _) in containersWithNoPropertiesSet)
            {
                ElideContainerShape(graph, container);
            }
        }

        // Removes a container shape, copying its shapes into its parent.
        // Does nothing if the container has no parent.
        void ElideContainerShape(ObjectGraph<Node> graph, CompositionContainerShape container)
        {
            // Insert the children into the parent.
            var parent = (IContainShapes)graph[container].Parent;
            if (parent == null)
            {
                // The container may have already been removed, or it might be a root.
                return;
            }

            // Find the index in the parent of the container.
            // If childCount is 1, just replace the the container in the parent.
            // If childCount is >1, insert into the parent.
            var index = parent.Shapes.IndexOf(container);

            if (index == -1)
            {
                // Container has already been removed.
                return;
            }

            // Get the children from the container.
            var children = container.Shapes;

            if (children.Count == 0)
            {
                // The container has no children. This is rare but can happen if
                // the container is for a layer type that we don't support.
                return;
            }

            GraphHasChanged();

            // Insert the first child where the container was.
            var child0 = children[0];

            CopyDescriptions(container, child0);

            parent.Shapes[index] = child0;

            // Fix the parent pointer in the graph.
            graph[child0].Parent = (CompositionObject)parent;

            // Insert the rest of the children.
            for (var n = 1; n < children.Count; n++)
            {
                var childN = children[n];

                CopyDescriptions(container, childN);

                parent.Shapes.Insert(index + n, childN);

                // Fix the parent pointer in the graph.
                graph[childN].Parent = (CompositionObject)parent;
            }

            // Remove the children from the container.
            container.Shapes.Clear();
        }

        // Removes a container visual, copying its children into its parent.
        // Does nothing if the container has no parent.
        bool TryElideContainerVisual(ObjectGraph<Node> graph, ContainerVisual container)
        {
            // Insert the children into the parent.
            var parent = (ContainerVisual)graph[container].Parent;
            if (parent == null)
            {
                // The container may have already been removed, or it might be a root.
                return false;
            }

            // Find the index in the parent of the container.
            // If childCount is 1, just replace the the container in the parent.
            // If childCount is >1, insert into the parent.
            var index = parent.Children.IndexOf(container);

            // Get the children from the container.
            var children = container.Children;

            if (container.Children.Count == 0)
            {
                // The container has no children. This is rare but can happen if
                // the container is for a layer type that we don't support.
                return true;
            }

            GraphHasChanged();

            // Insert the first child where the container was.
            var child0 = children[0];

            CopyDescriptions(container, child0);

            parent.Children[index] = child0;

            // Fix the parent pointer in the graph.
            graph[child0].Parent = parent;

            // Insert the rest of the children.
            for (var n = 1; n < children.Count; n++)
            {
                var childN = children[n];

                CopyDescriptions(container, childN);

                parent.Children.Insert(index + n, childN);

                // Fix the parent pointer in the graph.
                graph[childN].Parent = parent;
            }

            // Remove the children from the container.
            container.Children.Clear();

            return true;
        }

        // Finds ContainerShapes that only has it's Transform set, with a single child that
        // does not have its Transform set and pulls the child into the parent. This is OK to do
        // because the Transform will still be evaluated as if it is higher in the tree.
        void CoalesceContainerShapes2(
            ObjectGraph<Node> graph,
            (Node node, CompositionContainerShape container, IContainShapes parent)[] containerShapes)
        {
            var containerShapesWith1Container = containerShapes.Where(n =>
                    n.container.Shapes.Count == 1 &&
                    n.container.Shapes[0].Type == CompositionObjectType.CompositionContainerShape
                ).ToArray();

            foreach (var (_, container, _) in containerShapesWith1Container)
            {
                if (!container.Shapes.Any())
                {
                    // The children have already been removed.
                    continue;
                }

                var child = (CompositionContainerShape)container.Shapes[0];

                var parentProperties = GetNonDefaultProperties(container);
                var childProperties = GetNonDefaultProperties(child);

                if (parentProperties == PropertyId.TransformMatrix &&
                    (childProperties & PropertyId.TransformMatrix) == PropertyId.None)
                {
                    if (child.Animators.Any())
                    {
                        // Ignore if the child is animated. We could handle it but it's more complicated.
                        continue;
                    }

                    TransferShapeProperties(child, container);

                    // Move the child's children into the parent.
                    ElideContainerShape(graph, child);
                }
            }
        }

        // Find ContainerVisuals that have a single ShapeVisual child with orthongonal properties and
        // push the properties down to the ShapeVisual
        static void PushPropertiesDownToShapeVisual(ObjectGraph<Node> graph)
        {
            var shapeVisualsWithSingleParents = graph.CompositionObjectNodes.Where(n =>
                n.Object.Type == CompositionObjectType.ShapeVisual &&
                ((ContainerVisual)n.Node.Parent).Children.Count == 1).ToArray();

            foreach (var (node, obj) in shapeVisualsWithSingleParents)
            {
                var shapeVisual = (ShapeVisual)obj;
                var parent = (ContainerVisual)node.Parent;
                var parentProperties = GetNonDefaultProperties(parent);

                if (parentProperties == PropertyId.None)
                {
                    // No properties to push down.
                    continue;
                }

                // If the parent has no transforming properties, and a Size that
                // is the same as the Child's size, and a 0 InsetClip and none of
                // these properties is animated, the InsetClip and Size on the Visual
                // are redundant and can be removed.
                if ((parentProperties &
                        (PropertyId.CenterPoint | PropertyId.Offset |
                         PropertyId.RotationAngleInDegrees | PropertyId.Scale |
                         PropertyId.TransformMatrix)) == PropertyId.None &&
                    parent.Clip is InsetClip insetClip &&
                    IsNullOrZero(insetClip.CenterPoint) &&
                    IsNullOrOne(insetClip.Scale) &&
                    IsNullOrZero(insetClip.LeftInset) && IsNullOrZero(insetClip.RightInset) &&
                    IsNullOrZero(insetClip.TopInset) && IsNullOrZero(insetClip.BottomInset) &&
                    insetClip.Animators.Count == 0 &&
                    parent.Size == shapeVisual.Size &&
                    !IsPropertyAnimated(parent, PropertyId.Size) &&
                    !IsPropertyAnimated(shapeVisual, PropertyId.Size))
                {
                    parent.Clip = null;
                    parent.Size = null;
                }
            }
        }

        static bool IsNullOrZero(float? value) => value is null || value == 0;

        static bool IsNullOrOne(Vector2? value) => value is null || value == Vector2.One;

        static bool IsNullOrZero(Vector2? value) => value is null || value == Vector2.Zero;

        static bool IsPropertyAnimated(CompositionObject obj, PropertyId property)
        {
            var propertyName = property.ToString();
            return obj.Animators.Any(p => p.AnimatedProperty == propertyName);
        }

        // Finds ShapeVisuals with a single shape that has a visibility animation and
        // move the animation into the ShapeVisual.
        static void PushShapeTreeVisibilityIntoVisualTree(ObjectGraph<Node> graph)
        {
            var candidate =
                (from n in graph.CompositionObjectNodes
                 let sv = n.Object as ShapeVisual
                 where sv != null && sv.Shapes.Count == 1
                 let shape = sv.Shapes[0]
                 where IsScaleUsedForVisibility(shape)
                 select sv).ToArray();

            foreach (var visual in candidate)
            {
                // TODO
                // Get the visbility animation as a sequence of (bool,progress).
                // Remove the visibility animation from the shape.
                // Get the visibility animation from the shape visual as a sequence
                // Convert to a new visibility animation. Apply it to the shape visual
                var shape = visual.Shapes[0];
                var visualVisibility = GetVisiblityAnimationDescription(visual);
                var shapeVisibility = GetVisiblityAnimationDescription(shape);

                Debug.Assert(shapeVisibility.sequence.Length > 0, "Checked above");

                if (visualVisibility.sequence.Length == 0)
                {
                    // Easy case - the visual isn't being used for visibility.
                    var c = new Compositor();
                    var animation = c.CreateBooleanKeyFrameAnimation();
                    animation.Duration = shapeVisibility.duration;
                    if (shapeVisibility.sequence[0].progress == 0)
                    {
                        // Set the initial visiblity.
                        visual.IsVisible = shapeVisibility.sequence[0].isVisible;
                    }

                    foreach (var visibility in shapeVisibility.sequence)
                    {
                        if (visibility.progress == 0)
                        {
                            // The 0 progress value is already handled.
                            continue;
                        }
                        else
                        {
                            animation.InsertKeyFrame(visibility.progress, visibility.isVisible);
                        }
                    }

                    visual.StartAnimation("IsVisible", animation);

                    var progressAnimation = shape.TryGetAnimationController("Scale").Animators.Where(anim => anim.AnimatedProperty == "Progress").First().Animation;
                    var controller = visual.TryGetAnimationController("IsVisible");
                    controller.Pause();
                    controller.StartAnimation("Progress", progressAnimation);

                    // Clear out the Scale properties and animations from the shape.
                    shape.Scale = null;
                    shape.StopAnimation("Scale");
                }
            }
        }

        static (TimeSpan duration, (bool isVisible, float progress)[] sequence) GetVisiblityAnimationDescription(Visual visual)
        {
            // Get the visibility animation.
            // TODO - this needs to take the controller's Progress expression into account.
            var animator = visual.Animators.Where(anim => anim.AnimatedProperty == "IsVisible").FirstOrDefault();

            if (animator is null)
            {
                return (TimeSpan.Zero, Array.Empty<(bool, float)>());
            }

            var visibilityAnimation = (BooleanKeyFrameAnimation)animator.Animation;

            return (visibilityAnimation.Duration, GetDescription().ToArray());

            IEnumerable<(bool isVisible, float progress)> GetDescription()
            {
                if (animator is null)
                {
                    // Not animated, or it uses an expression so we can't deal with it.
                    yield break;
                }

                var firstSeen = false;

                foreach (KeyFrameAnimation<bool, Expr.Boolean>.ValueKeyFrame kf in visibilityAnimation.KeyFrames)
                {
                    if (!firstSeen)
                    {
                        firstSeen = true;
                        if (kf.Progress != 0 && visual.IsVisible.HasValue && !visual.IsVisible.Value)
                        {
                            yield return (false, 0);
                        }

                        yield return (kf.Value, kf.Progress);
                    }
                }
            }
        }

        static (TimeSpan duration, (bool isVisible, float progress)[] sequence) GetVisiblityAnimationDescription(CompositionShape shape)
        {
            var scaleValue = shape.Scale;

            if (scaleValue.HasValue && scaleValue.Value != Vector2.One && scaleValue.Value != Vector2.Zero)
            {
                // The animation is not used for visibility. Precondition.
                throw new InvalidOperationException();
            }

            var scaleAnimator = shape.Animators.Where(anim => anim.AnimatedProperty == "Scale").FirstOrDefault();

            if (scaleAnimator is null)
            {
                // The animation is not used for visibility. Precondition.
                throw new InvalidOperationException();
            }

            var firstSeen = false;
            var scaleAnimation = (Vector2KeyFrameAnimation)scaleAnimator.Animation;

            return (scaleAnimation.Duration, GetDescription().ToArray());

            IEnumerable<(bool isVisible, float progress)> GetDescription()
            {
                foreach (KeyFrameAnimation<Vector2, Expr.Vector2>.ValueKeyFrame kf in scaleAnimation.KeyFrames)
                {
                    if (kf.Easing.Type != CompositionObjectType.StepEasingFunction)
                    {
                        // The animation is not used for visibility. Precondition.
                        throw new InvalidOperationException();
                    }

                    if (kf.Value != Vector2.One && kf.Value != Vector2.Zero)
                    {
                        // The animation is not used for visibility. Precondition.
                        throw new InvalidOperationException();
                    }

                    if (!firstSeen)
                    {
                        firstSeen = true;
                        if (kf.Progress != 0 && shape.Scale.HasValue && shape.Scale.Value != Vector2.One)
                        {
                            yield return (false, 0);
                        }

                        yield return (kf.Value == Vector2.One, kf.Progress);
                    }
                }
            }
        }

        // Finds container shapes with a single child and have only Scale properties set for visibility animations
        // and pushes the scale property and animation down.
        void PushShapeVisbilityDown(
                        ObjectGraph<Node> graph,
                        (Node node, CompositionContainerShape container, IContainShapes parent)[] containerShapes)
        {
            var containerShapesWith1Child = containerShapes.Where(n =>
                    n.container.Shapes.Count == 1
                ).ToArray();

            foreach (var (_, parent, _) in containerShapesWith1Child)
            {
                if (!parent.Shapes.Any())
                {
                    // The children have already been removed.
                    continue;
                }

                var child = parent.Shapes[0];

                var parentProperties = GetNonDefaultProperties(parent);
                var childProperties = GetNonDefaultProperties(child);

                if (parentProperties == PropertyId.Scale && IsScaleUsedForVisibility(parent))
                {
                    // The parent is only used for visibility (via its Scale property).
                    // This can be safely pushed up or down the tree.
                    if ((childProperties & PropertyId.Scale) == PropertyId.None)
                    {
                        // The child does not use Scale. Move the Scale down to the child.
                        TransferShapeProperties(parent, child);

                        ElideContainerShape(graph, parent);
                    }
                }
            }
        }

        // Find ContainerShapes that have a single SpriteShape with orthongonal properties
        // and remove the ContainerShape.
        void PushPropertiesDownToSpriteShape(
            ObjectGraph<Node> graph,
            (Node node, CompositionContainerShape container, IContainShapes parent)[] containerShapes)
        {
            var containerShapesWith1Sprite = containerShapes.Where(n =>
                    n.container.Shapes.Count == 1 &&
                    n.container.Shapes[0].Type == CompositionObjectType.CompositionSpriteShape
                ).ToArray();

            foreach (var (_, parent, _) in containerShapesWith1Sprite)
            {
                if (!parent.Shapes.Any())
                {
                    // The children have already been removed.
                    continue;
                }

                var child = (CompositionSpriteShape)parent.Shapes[0];

                var parentProperties = GetNonDefaultProperties(parent);
                var childProperties = GetNonDefaultProperties(child);

                // Common case is that the child has no non-default properties.
                // We could handle more cases but it's more complicated.
                if (childProperties == PropertyId.None)
                {
                    if ((parentProperties & PropertyId.Properties) != PropertyId.None)
                    {
                        // Ignore if the parent has PropertySet propeties. We could handle it but it's more complicated.
                        continue;
                    }

                    // Copy the parent's properties onto the child and remove the parent.
                    TransferShapeProperties(parent, child);

                    ElideContainerShape(graph, parent);
                }
            }
        }

        static bool IsScaleUsedForVisibility(CompositionShape shape)
        {
            var scaleValue = shape.Scale;

            if (scaleValue.HasValue && scaleValue.Value != Vector2.One && scaleValue.Value != Vector2.Zero)
            {
                // Scale has a value that is not invisible (0,0) and it's not identity (1,1).
                return false;
            }

            var scaleAnimator = shape.Animators.Where(anim => anim.AnimatedProperty == "Scale").FirstOrDefault();

            if (scaleAnimator is null)
            {
                return false;
            }

            var scaleAnimation = (Vector2KeyFrameAnimation)scaleAnimator.Animation;
            foreach (var kf in scaleAnimation.KeyFrames)
            {
                if (kf.Easing.Type != CompositionObjectType.StepEasingFunction)
                {
                    return false;
                }

                if (kf.Type != KeyFrameType.Value)
                {
                    return false;
                }

                var keyFrameValue = ((KeyFrameAnimation<Vector2, Expr.Vector2>.ValueKeyFrame)kf).Value;

                if (keyFrameValue != Vector2.One && keyFrameValue != Vector2.Zero)
                {
                    return false;
                }
            }

            return true;
        }

        void CoalesceContainerVisuals(ObjectGraph<Node> graph)
        {
            // If a container is not animated and has no properties set, its children can be inserted into its parent.
            var containersWithNoPropertiesSet = graph.CompositionObjectNodes.Where(n =>

                    // Find the ContainerVisuals that have no properties set.
                    n.Object.Type == CompositionObjectType.ContainerVisual &&
                    GetNonDefaultProperties((ContainerVisual)n.Object) == PropertyId.None
            ).ToArray();

            // Pull the children of the container into the parent of the container. Remove the unnecessary containers.
            foreach (var (node, obj) in containersWithNoPropertiesSet)
            {
                var container = (ContainerVisual)obj;

                // Insert the children into the parent.
                var parent = (ContainerVisual)node.Parent;
                if (parent == null)
                {
                    // The container may have already been removed, or it might be a root.
                    continue;
                }

                // Find the index in the parent of the container.
                // If childCount is 1, just replace the the container in the parent.
                // If childCount is >1, insert into the parent.
                var index = parent.Children.IndexOf(container);

                var children = container.Children;

                // Get the children from the container.
                if (children.Count == 0)
                {
                    // The container has no children. This is rare but can happen if
                    // the container is for a layer type that we don't support.
                    continue;
                }

                GraphHasChanged();

                // Insert the first child where the container was.
                var child0 = children[0];

                CopyDescriptions(container, child0);

                parent.Children[index] = child0;

                // Fix the parent pointer in the graph.
                graph[child0].Parent = parent;

                // Insert the rest of the children.
                for (var n = 1; n < children.Count; n++)
                {
                    var childN = children[n];

                    CopyDescriptions(container, childN);

                    parent.Children.Insert(index + n, childN);

                    // Fix the parent pointer in the graph.
                    graph[childN].Parent = parent;
                }

                // Remove the children from the container.
                children.Clear();
            }
        }

        static PropertyId GetNonDefaultProperties(CompositionShape obj)
        {
            var result = PropertyId.None;
            if (obj.CenterPoint.HasValue)
            {
                result |= PropertyId.CenterPoint;
            }

            if (obj.Comment != null)
            {
                result |= PropertyId.Comment;
            }

            if (obj.Offset.HasValue)
            {
                result |= PropertyId.Offset;
            }

            if (obj.Properties.Names.Count != 0)
            {
                result |= PropertyId.Properties;
            }

            if (obj.RotationAngleInDegrees.HasValue)
            {
                result |= PropertyId.RotationAngleInDegrees;
            }

            if (obj.Scale.HasValue)
            {
                result |= PropertyId.Scale;
            }

            if (obj.TransformMatrix.HasValue)
            {
                result |= PropertyId.TransformMatrix;
            }

            foreach (var animator in obj.Animators)
            {
                result |= PropertyIdFromName(animator.AnimatedProperty);
            }

            return result;
        }

        static PropertyId GetNonDefaultProperties(Visual obj)
        {
            var result = PropertyId.None;
            if (obj.BorderMode.HasValue)
            {
                result |= PropertyId.BorderMode;
            }

            if (obj.CenterPoint.HasValue)
            {
                result |= PropertyId.CenterPoint;
            }

            if (obj.Clip != null)
            {
                result |= PropertyId.Clip;
            }

            if (obj.Comment != null)
            {
                result |= PropertyId.Comment;
            }

            if (obj.Offset.HasValue)
            {
                result |= PropertyId.Offset;
            }

            if (obj.Opacity.HasValue)
            {
                result |= PropertyId.Opacity;
            }

            if (obj.Properties.Names.Count != 0)
            {
                result |= PropertyId.Properties;
            }

            if (obj.RotationAngleInDegrees.HasValue)
            {
                result |= PropertyId.RotationAngleInDegrees;
            }

            if (obj.RotationAxis.HasValue)
            {
                result |= PropertyId.RotationAxis;
            }

            if (obj.Scale.HasValue)
            {
                result |= PropertyId.Scale;
            }

            if (obj.Size.HasValue)
            {
                result |= PropertyId.Size;
            }

            if (obj.TransformMatrix.HasValue)
            {
                result |= PropertyId.TransformMatrix;
            }

            foreach (var animator in obj.Animators)
            {
                result |= PropertyIdFromName(animator.AnimatedProperty);
            }

            return result;
        }

        // If a ContainerVisual has exactly one child that is a ContainerVisual, and each
        // affects different sets of properties then they can be combined into one.
        void CoalesceOrthogonalContainerVisuals(ObjectGraph<Node> graph)
        {
            // If a container is not animated and has no properties set, its children can be inserted into its parent.
            var containersWithASingleContainer = graph.CompositionObjectNodes.Where(n =>
            {
                // Find the ContainerVisuals that have a single child that is a ContainerVisual.
                return
                    n.Object is ContainerVisual container &&
                    container.Children.Count == 1 &&
                    container.Children[0].Type == CompositionObjectType.ContainerVisual;
            }).ToArray();

            foreach (var (_, obj) in containersWithASingleContainer)
            {
                var parent = (ContainerVisual)obj;
                if (parent.Children.Count != 1)
                {
                    // The previous iteration of the loop modified the Children list.
                    continue;
                }

                var child = (ContainerVisual)parent.Children[0];

                var parentProperties = GetNonDefaultProperties(parent);
                var childProperties = GetNonDefaultProperties(child);

                // If the containers have non-overlapping properties they can be coalesced.
                // If the child has PropertySet values, don't try to coalesce (although we could
                // move the properties, we're not supporting that case for now.).
                if (ArePropertiesOrthogonal(parentProperties, childProperties) &&
                    (childProperties & PropertyId.Properties) == PropertyId.None)
                {
                    if (IsVisualSurfaceRoot(graph, parent))
                    {
                        // VisualSurface roots are special - they ignore their transforming properties
                        // so such properties cannot be hoisted from the child.
                        continue;
                    }

                    // Move the children of the child into the parent, and set the child's
                    // properties and animations on the parent.
                    if (TryElideContainerVisual(graph, child))
                    {
                        TransferContainerVisualProperties(from: child, to: parent);
                    }
                }
            }
        }

        // True iff the given ContainerVisual is the SourceVisual of a CompositionVisualSurface.
        // In this case the transforming properties (e.g. offset) will be ignored, so it is not
        // safe to hoist any such properties from its child.
        static bool IsVisualSurfaceRoot(ObjectGraph<Node> graph, ContainerVisual containerVisual)
         => graph[containerVisual].InReferences.Any(vertex => vertex.Node.Object is CompositionVisualSurface);

        static bool ArePropertiesOrthogonal(PropertyId parent, PropertyId child)
        {
            if ((parent & child) != PropertyId.None)
            {
                // The properties overlap.
                return false;
            }

            // The properties do not overlap. But we have to check for some properties that
            // need to be evaluated in a particular order, which means they cannot be just
            // moved between the child and parent.
            if ((parent & (PropertyId.Color | PropertyId.Opacity | PropertyId.Path)) == parent ||
                (child & (PropertyId.Color | PropertyId.Opacity | PropertyId.Path)) == child)
            {
                // These properties are not order dependent.
                return true;
            }

            // Evaluation order is TransformMatrix, Offset, Rotation, Scale. So if the
            // child has a transform it can not be pulled into the parent if the parent
            // has offset, rotation, scale, clip, or centerpoint because it would cause
            // the transform to be evaluated too early.
            if (((child & PropertyId.TransformMatrix) != PropertyId.None) &&
                ((parent & (PropertyId.Offset | PropertyId.RotationAngleInDegrees | PropertyId.Scale | PropertyId.Clip | PropertyId.CenterPoint)) != PropertyId.None))
            {
                return false;
            }

            if (((parent & PropertyId.RotationAngleInDegrees) != PropertyId.None) &&
                ((child & (PropertyId.Offset | PropertyId.Clip)) != PropertyId.None))
            {
                return false;
            }

            if (((parent & PropertyId.Scale) != PropertyId.None) &&
                ((child & (PropertyId.Offset | PropertyId.RotationAngleInDegrees | PropertyId.Clip)) != PropertyId.None))
            {
                return false;
            }

            return true;
        }

        // If a ContainerVisual has exactly one child that is a SpriteVisual or ShapeVisual, and each
        // affects different sets of properties then properties from the container can be
        // copied into the SpriteVisual and the container can be removed.
        void CoalesceOrthogonalVisuals(ObjectGraph<Node> graph)
        {
            // If a container is not animated and has no properties set, its children can be inserted into its parent.
            var containersWithASingleSprite = graph.CompositionObjectNodes.Where(n =>
            {
                // Find the ContainerVisuals that have a single child that is a ContainerVisual.
                return
                    n.Object is ContainerVisual container &&
                    n.Node.Parent is ContainerVisual &&
                    container.Children.Count == 1 &&
                    (container.Children[0].Type == CompositionObjectType.SpriteVisual ||
                     container.Children[0].Type == CompositionObjectType.ShapeVisual);
            }).ToArray();

            foreach (var (node, obj) in containersWithASingleSprite)
            {
                var parent = (ContainerVisual)obj;
                var child = (ContainerVisual)parent.Children[0];

                var parentProperties = GetNonDefaultProperties(parent);
                var childProperties = GetNonDefaultProperties(child);

                // If the containers have non-overlapping properties they can be coalesced.
                // If the parent has PropertySet values, don't try to coalesce (although we could
                // move the properties, we're not supporting that case for now.).
                if (ArePropertiesOrthogonal(parentProperties, childProperties) &&
                    (parentProperties & PropertyId.Properties) == PropertyId.None)
                {
                    if (IsVisualSurfaceRoot(graph, parent))
                    {
                        // VisualSurface roots are special - they ignore their transforming properties
                        // so such properties cannot be hoisted from the child.
                        continue;
                    }

                    // Copy the values of the non-default properties from the parent to the child.
                    if (TryElideContainerVisual(graph, parent))
                    {
                        TransferContainerVisualProperties(from: parent, to: child);
                    }
                }
            }
        }

        static void TransferShapeProperties(CompositionShape from, CompositionShape to)
        {
            if (from.CenterPoint.HasValue)
            {
                to.CenterPoint = from.CenterPoint;
            }

            if (from.Comment != null)
            {
                to.Comment = from.Comment;
            }

            if (from.Offset.HasValue)
            {
                to.Offset = from.Offset;
            }

            if (from.RotationAngleInDegrees.HasValue)
            {
                to.RotationAngleInDegrees = from.RotationAngleInDegrees;
            }

            if (from.Scale.HasValue)
            {
                to.Scale = from.Scale;
            }

            if (from.TransformMatrix.HasValue)
            {
                to.TransformMatrix = from.TransformMatrix;
            }

            // Start the from's animations on the to.
            foreach (var anim in from.Animators)
            {
                to.StartAnimation(anim.AnimatedProperty, anim.Animation);
                if (anim.Controller.IsPaused || anim.Controller.Animators.Count > 0)
                {
                    var controller = to.TryGetAnimationController(anim.AnimatedProperty);
                    if (anim.Controller.IsPaused)
                    {
                        controller.Pause();
                    }

                    foreach (var controllerAnim in anim.Controller.Animators)
                    {
                        controller.StartAnimation(controllerAnim.AnimatedProperty, controllerAnim.Animation);
                    }
                }
            }
        }

        static void TransferContainerVisualProperties(ContainerVisual from, ContainerVisual to)
        {
            if (from.BorderMode.HasValue)
            {
                to.BorderMode = from.BorderMode;
            }

            if (from.CenterPoint.HasValue)
            {
                to.CenterPoint = from.CenterPoint;
            }

            if (from.Clip != null)
            {
                to.Clip = from.Clip;
            }

            if (from.Comment != null)
            {
                to.Comment = from.Comment;
            }

            if (from.IsVisible.HasValue)
            {
                to.IsVisible = from.IsVisible;
            }

            if (from.Offset.HasValue)
            {
                to.Offset = from.Offset;
            }

            if (from.Opacity.HasValue)
            {
                to.Opacity = from.Opacity;
            }

            if (from.RotationAngleInDegrees.HasValue)
            {
                to.RotationAngleInDegrees = from.RotationAngleInDegrees;
            }

            if (from.RotationAxis.HasValue)
            {
                to.RotationAxis = from.RotationAxis;
            }

            if (from.Scale.HasValue)
            {
                to.Scale = from.Scale;
            }

            if (from.Size.HasValue)
            {
                to.Size = from.Size;
            }

            if (from.TransformMatrix.HasValue)
            {
                to.TransformMatrix = from.TransformMatrix;
            }

            // Start the from's animations on the to.
            foreach (var anim in from.Animators)
            {
                to.StartAnimation(anim.AnimatedProperty, anim.Animation);
                if (anim.Controller.IsPaused || anim.Controller.Animators.Count > 0)
                {
                    var controller = to.TryGetAnimationController(anim.AnimatedProperty);
                    if (anim.Controller.IsPaused)
                    {
                        controller.Pause();
                    }

                    foreach (var controllerAnim in anim.Controller.Animators)
                    {
                        controller.StartAnimation(controllerAnim.AnimatedProperty, controllerAnim.Animation);
                    }
                }
            }
        }

        [Flags]
        enum PropertyId
        {
            None = 0,
            Unknown = 1,
            BorderMode = Unknown << 1,
            CenterPoint = BorderMode << 1,
            Clip = CenterPoint << 1,
            Color = Clip << 1,
            Comment = Color << 1,
            IsVisible = Comment << 1,
            Offset = IsVisible << 1,
            Opacity = Offset << 1,
            Path = Opacity << 1,
            Position = Path << 1,
            Progress = Position << 1,
            Properties = Progress << 1,
            RotationAngleInDegrees = Properties << 1,
            RotationAxis = RotationAngleInDegrees << 1,
            Scale = RotationAxis << 1,
            Size = Scale << 1,
            TransformMatrix = Size << 1,
            TrimStart = TransformMatrix << 1,
            TrimEnd = TrimStart << 1,
        }

        static PropertyId PropertyIdFromName(string value)
            => value switch
            {
                "BorderMode" => PropertyId.BorderMode,
                "CenterPoint" => PropertyId.CenterPoint,
                "Clip" => PropertyId.Clip,
                "Color" => PropertyId.Color,
                "Comment" => PropertyId.Comment,
                "IsVisible" => PropertyId.IsVisible,
                "Offset" => PropertyId.Offset,
                "Opacity" => PropertyId.Opacity,
                "Path" => PropertyId.Path,
                "Position" => PropertyId.Position,
                "Progress" => PropertyId.Progress,
                "RotationAngleInDegrees" => PropertyId.RotationAngleInDegrees,
                "RotationAxis" => PropertyId.RotationAxis,
                "Scale" => PropertyId.Scale,
                "Size" => PropertyId.Size,
                "TransformMatrix" => PropertyId.TransformMatrix,
                "TrimStart" => PropertyId.TrimStart,
                "TrimEnd" => PropertyId.TrimEnd,
                _ => PropertyId.Unknown,
            };

        void CopyDescriptions(IDescribable from, IDescribable to)
        {
            GraphHasChanged();

            // Copy the short description. This may lose some information
            // in the "to" but generally that same information is in the
            // "from" description anyway.
            var fromShortDescription = from.ShortDescription;
            if (!string.IsNullOrWhiteSpace(fromShortDescription))
            {
                to.ShortDescription = fromShortDescription;
            }

            // Do not try to append the long description - it's impossible to do
            // a reasonable job of combining 2 long descriptions. But if the "to"
            // object doesn't already have a long description, copy the long
            // description from the "from" object.
            var toLongDescription = to.LongDescription;
            if (string.IsNullOrWhiteSpace(toLongDescription))
            {
                var fromLongDescription = from.LongDescription;
                if (!string.IsNullOrWhiteSpace(fromLongDescription))
                {
                    to.LongDescription = fromLongDescription;
                }
            }

            // If the "from" object has a name and the "to" object does not,
            // copy the name. For any other case it's not clear what we should
            // do, so just leave the name as it was.
            var fromName = from.Name;
            if (!string.IsNullOrWhiteSpace(fromName))
            {
                if (string.IsNullOrWhiteSpace(to.Name))
                {
                    to.Name = fromName;
                }
            }
        }

        static void AppendShortDescription(IDescribable obj, string description)
        {
            obj.ShortDescription = $"{obj.ShortDescription} {description}";
        }

        static void AppendLongDescription(IDescribable obj, string description)
        {
            obj.LongDescription = $"{obj.LongDescription} {description}";
        }

        static string DescribeTransform(Vector2 scale, double rotationDegrees, Vector2 offset)
        {
            var sb = new StringBuilder();
            if (scale != Vector2.One)
            {
                sb.Append($"Scale:{scale.X}");
            }

            if (rotationDegrees != 0)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append($"RotationDegrees:{rotationDegrees}");
            }

            if (offset != Vector2.Zero)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append($"Offset:{offset}");
            }

            return sb.ToString();
        }

        static string DescribeTransform(Vector3 scale, double rotationDegrees, Vector3 offset)
        {
            var sb = new StringBuilder();
            if (scale != Vector3.One)
            {
                sb.Append($"Scale({scale.X},{scale.Y},{scale.Z})");
            }

            if (rotationDegrees != 0)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append($"RotationDegrees({rotationDegrees})");
            }

            if (offset != Vector3.Zero)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append($"Offset({offset.X},{offset.Y},{offset.Z})");
            }

            return sb.ToString();
        }

        sealed class Node : Graph.Node<Node>
        {
            internal CompositionObject Parent { get; set; }
        }
    }
}
