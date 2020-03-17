// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools
{
    /// <summary>
    /// Optimizes a <see cref="Visual"/> tree by combining and removing containers and
    /// removing containers that are empty.
    /// </summary>
    static class TreeReducer
    {
        internal static Visual OptimizeContainers(Visual root)
        {
            // Running the optimization multiple times can improve the results.
            // Keep iterating as long as the number of nodes is reducing.
            var graph = ObjectGraph<Node>.FromCompositionObject(root, includeVertices: true);
            var beforeCount = int.MaxValue;
            var afterCount = graph.Nodes.Count();

            while (afterCount < beforeCount)
            {
                beforeCount = afterCount;

                Optimize(graph);

                // Rebuild the graph.
                graph = ObjectGraph<Node>.FromCompositionObject(root, includeVertices: true);
                afterCount = graph.Nodes.Count();
            }

            return root;
        }

        static void Optimize(ObjectGraph<Node> graph)
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

        static void OptimizeVisuals(ObjectGraph<Node> graph)
        {
            CoalesceContainerVisuals(graph);
            CoalesceOrthogonalVisuals(graph);
            CoalesceOrthogonalContainerVisuals(graph);
            RemoveRedundantInsetClipVisuals(graph);
        }

        static void OptimizeShapes(ObjectGraph<Node> graph)
        {
            ElideTransparentSpriteShapes(graph);
            OptimizeContainerShapes(graph);
        }

        static void OptimizeContainerShapes(ObjectGraph<Node> graph)
        {
            var containerShapes =
                (from pair in graph.CompositionObjectNodes
                 where pair.Object.Type == CompositionObjectType.CompositionContainerShape
                 let parent = (IContainShapes)pair.Node.Parent
                 select (node: pair.Node, container: (CompositionContainerShape)pair.Object, parent)).ToArray();

            /*PushShapeVisibilityUp(graph);*/
            ElideEmptyContainerShapes(graph, containerShapes);
            ElideStructuralContainerShapes(graph, containerShapes);
            PushContainerShapeTransformsDown(graph, containerShapes);
            CoalesceContainerShapes2(graph, containerShapes);
            PushPropertiesDownToSpriteShape(graph, containerShapes);
        }

        // Finds ContainerShapes that only exist to control visibility and if there are multiple of
        // them at the same level, replace them with a single ContainerShape.
        static void PushShapeVisibilityUp(ObjectGraph<Node> graph)
        {
            var children1 = graph.CompositionObjectNodes.Where(n =>
                n.Object is IContainShapes shapeContainer &&
                shapeContainer.Shapes.Count > 1
            ).ToArray();

            foreach (var ch in children1)
            {
                var container = (IContainShapes)ch.Object;
                var grouped = GroupSimilarChildContainers(container).ToArray();

                if (grouped.Any(g => g.Length > 1))
                {
                    // There was some grouping. Clear out the children and replace them.
                    container.Shapes.Clear();
                    foreach (var group in grouped)
                    {
                        var first = group[0];
                        container.Shapes.Add(first);

                        if (group.Length > 1)
                        {
                            // All of the items in the group will share the first container.
                            var firstContainer = (CompositionContainerShape)first;
                            for (var i = 1; i < group.Length; i++)
                            {
                                // Move the first child of each of the other containers into this container.
                                var groupI = (CompositionContainerShape)group[i];

                                // Check the the child still exists - it may have been elided already.
                                if (groupI.Shapes.Count > 0)
                                {
                                    firstContainer.Shapes.Add(groupI.Shapes[0]);
                                }
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
                        yield return grouped.ToArray();
                        grouped.Clear();
                    }

                    yield return new[] { child };
                }
                else
                {
                    // It's a container.
                    if (grouped.Count == 0)
                    {
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
        static void RemoveRedundantInsetClipVisuals(ObjectGraph<Node> graph)
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
                if (clip.TopInset != 0 ||
                    clip.RightInset != 0 ||
                    clip.LeftInset != 0 ||
                    clip.BottomInset != 0)
                {
                    continue;
                }

                if (clip.Scale != Vector2.One)
                {
                    continue;
                }

                if (clip.CenterPoint != Vector2.Zero)
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
        static void SimplifyProperties(ObjectGraph<Node> graph)
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
        static void SimplifyProperties(Visual obj)
        {
            if (obj.CenterPoint.HasValue &&
                !obj.Scale.HasValue &&
                !obj.RotationAngleInDegrees.HasValue &&
                !obj.Animators.Where(a => a.AnimatedProperty == nameof(obj.Scale) || a.AnimatedProperty == nameof(obj.RotationAngleInDegrees)).Any())
            {
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
                    var transformDescription = DescribeTransform(scale, rotation, offset);
                    AppendShortDescription(obj, transformDescription);
                    AppendLongDescription(obj, transformDescription);
                    obj.TransformMatrix = combinedMatrix;
                }
            }
        }

        // Remove the centerpoint property if it's redundant, and convert properties to TransformMatrix if possible.
        static void SimplifyProperties(CompositionShape obj)
        {
            // Remove the centerpoint if it's not used by Scale or Rotation.
            if (obj.CenterPoint.HasValue &&
                !obj.Scale.HasValue &&
                !obj.RotationAngleInDegrees.HasValue &&
                !obj.Animators.Where(a => a.AnimatedProperty == nameof(obj.Scale) || a.AnimatedProperty == nameof(obj.RotationAngleInDegrees)).Any())
            {
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
                    var transformDescription = DescribeTransform(scale, rotation, offset);
                    AppendShortDescription(obj, transformDescription);
                    AppendLongDescription(obj, transformDescription);
                    obj.TransformMatrix = combinedMatrix;
                }
            }
        }

        static float DegreesToRadians(float angle) => (float)(Math.PI * angle / 180.0);

        static bool IsBrushTransparent(CompositionBrush brush)
        {
            return brush == null || (!brush.Animators.Any() && (brush as CompositionColorBrush)?.Color?.A == 0);
        }

        static void ElideTransparentSpriteShapes(ObjectGraph<Node> graph)
        {
            var transparentShapes =
                (from pair in graph.CompositionObjectNodes
                 where pair.Object.Type == CompositionObjectType.CompositionSpriteShape
                 let shape = (CompositionSpriteShape)pair.Object
                 where IsBrushTransparent(shape.FillBrush) && IsBrushTransparent(shape.StrokeBrush)
                 select (Shape: shape, Parent: (IContainShapes)pair.Node.Parent)).ToArray();

            foreach (var pair in transparentShapes)
            {
                pair.Parent.Shapes.Remove(pair.Shape);
            }
        }

        // Removes any CompositionContainerShapes that have no children.
        static void ElideEmptyContainerShapes(
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

        static void PushContainerShapeTransformsDown(
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

        static void ElideStructuralContainerShapes(
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
        static void ElideContainerShape(ObjectGraph<Node> graph, CompositionContainerShape container)
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
        static bool TryElideContainerVisual(ObjectGraph<Node> graph, ContainerVisual container)
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
        static void CoalesceContainerShapes2(
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

        // Find ContainerShapes that have a single SpriteShape with orthongonal properties
        // and remove the ContainerShape.
        static void PushPropertiesDownToSpriteShape(
            ObjectGraph<Node> graph,
            (Node node, CompositionContainerShape container, IContainShapes parent)[] containerShapes)
        {
            var containerShapesWith1Sprite = containerShapes.Where(n =>
                    n.container.Shapes.Count == 1 &&
                    n.container.Shapes[0].Type == CompositionObjectType.CompositionSpriteShape
                ).ToArray();

            foreach (var (_, container, _) in containerShapesWith1Sprite)
            {
                if (!container.Shapes.Any())
                {
                    // The children have already been removed.
                    continue;
                }

                var child = (CompositionSpriteShape)container.Shapes[0];

                var parentProperties = GetNonDefaultProperties(container);
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
                    TransferShapeProperties(container, child);

                    ElideContainerShape(graph, container);
                }
            }
        }

        static void CoalesceContainerVisuals(ObjectGraph<Node> graph)
        {
            // If a container is not animated and has no properties set, its children can be inserted into its parent.
            var containersWithNoPropertiesSet = graph.CompositionObjectNodes.Where(n =>

                    // Find the ContainerVisuals that have no properties set.
                    n.Object.Type == CompositionObjectType.ContainerVisual &&
                    GetNonDefaultProperties((ContainerVisual)n.Object) == PropertyId.None
            ).ToArray();

            // Pull the children of the container into the parent of the container. Remove the unnecessary containers.
            foreach (var (Node, Object) in containersWithNoPropertiesSet)
            {
                var container = (ContainerVisual)Object;

                // Insert the children into the parent.
                var parent = (ContainerVisual)Node.Parent;
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

        static PropertyId GetNonDefaultProperties(ContainerVisual obj)
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
        static void CoalesceOrthogonalContainerVisuals(ObjectGraph<Node> graph)
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
        static void CoalesceOrthogonalVisuals(ObjectGraph<Node> graph)
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

        static void CopyDescriptions(IDescribable from, IDescribable to)
        {
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
