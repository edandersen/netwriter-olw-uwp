// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Xml;
using Windows.Data.Xml.Dom;
using BlogWriter.OpenLiveWriter;
using OpenLiveWriter.CoreServices;
using OpenLiveWriter.Extensibility.BlogClient;
using XmlAttribute = Windows.Data.Xml.Dom.XmlAttribute;
using XmlDocument = Windows.Data.Xml.Dom.XmlDocument;
using XmlElement = Windows.Data.Xml.Dom.XmlElement;

namespace OpenLiveWriter.BlogClient.Clients
{
	// see http://rakaz.nl/item/moving_from_atom_03_to_10
	public abstract class AtomProtocolVersion
	{
	    private static AtomProtocolVersion v03 = new Atom03ProtocolVersion();
        public static AtomProtocolVersion V03
        {
            get
            {
                return v03;
            }
            private set
            {
                v03 = value;
            }
        }
	    private static AtomProtocolVersion v10 = new Atom10ProtocolVersion();
        public static AtomProtocolVersion V10
        {
            get
            {
                return v10;
            }
            private set
            {
                v10 = value;
            }
        }
	    private static AtomProtocolVersion v10Draft = new Atom10DraftProtocolVersion();
        public static AtomProtocolVersion V10Draft
        {
            get
            {
                return v10Draft;
            }
            private set
            {
                v10Draft = value;
            }
        }
	    private static AtomProtocolVersion v10DraftBlogger = new Atom10DraftBloggerProtocolVersion();
        public static AtomProtocolVersion V10DraftBlogger
        {
            get
            {
                return v10DraftBlogger;
            }
            private set
            {
                v10DraftBlogger = value;
            }
        }

		public abstract string NamespaceUri { get; }
        public abstract string AtomPubNamespaceUri { get; }
        public abstract string PubNamespaceUri { get; }
		public abstract string UpdatedElName { get; }
		public abstract string PublishedElName { get; }
		
		public abstract XmlElement CreateCategoryElement(XmlDocument ownerDoc, string category, string categoryScheme, string categoryLabel);
		public abstract void RemoveAllCategories(IXmlNode entryNode, string categoryScheme, Uri documentUri);
		public abstract BlogPostCategory[] ExtractCategories(XmlElement entry, string categoryScheme, Uri documentUri);

		public abstract string TextNodeToHtml(IXmlNode node);
		public abstract string TextNodeToPlaintext(IXmlNode node);
		public abstract XmlElement HtmlToTextNode(XmlDocument ownerDoc, string html);
		public abstract XmlElement PlaintextToTextNode(XmlDocument ownerDoc, string text);


		private class Atom03ProtocolVersion : AtomProtocolVersion
		{
			private const string DC_URI = "http://purl.org/dc/elements/1.1/";

			public override string NamespaceUri { get { return "http://purl.org/atom/ns#"; } }
			public override string AtomPubNamespaceUri { get { return "http://purl.org/atom/app#"; } }
			public override string PubNamespaceUri { get { return "http://example.net/appns/"; } }
			public override string UpdatedElName { get { return "modified"; } }
			public override string PublishedElName { get { return "issued"; } }

			public override XmlElement CreateCategoryElement(XmlDocument ownerDoc, string category, string categoryScheme, string categoryLabel)
			{
				XmlElement element = ownerDoc.CreateElementNS(DC_URI, "dc:subject");
				element.InnerText = category;
				return element;
			}
			
			public override void RemoveAllCategories(IXmlNode node, string categoryScheme, Uri documentUri)
			{
				XmlNamespaceManager nsMgr = new XmlNamespaceManager(new NameTable());
				nsMgr.AddNamespace("dc", DC_URI);
				IXmlNode category;
				while (null != (category = node.SelectSingleNodeNS("dc:subject", nsMgr.ToNSMethodFormat())))
					category.ParentNode.RemoveChild(category);
			}


			public override string TextNodeToHtml(IXmlNode node)
			{
				return ToTextValue(node).ToHTML();
			}

			public override string TextNodeToPlaintext(IXmlNode node)
			{
				return ToTextValue(node).ToText();
			}

			private static AtomContentValue ToTextValue(IXmlNode node)
			{
				string type = (string) node.Attributes.SingleOrDefault(a => a.NodeName == "type").NodeValue;
				
				string mode = (string) node.Attributes.SingleOrDefault(a => a.NodeName == "mode")?.NodeValue;
				if (string.IsNullOrEmpty(mode))
					mode = "xml";

				string content;
				switch (mode)
				{
					case "escaped":
						content = node.InnerText;
						break;
					case "base64":
						content = Encoding.UTF8.GetString(Convert.FromBase64String((string)node.NodeValue));
						break;
					default:
					case "xml":
				        content = node.InnerText;
						if (type == string.Empty && node.SelectSingleNode("./*") != null)
							type = "application/xhtml+xml";
						break;
				}

				AtomContentValue tv;
				switch (type)
				{
					case "text/html":
						tv = new AtomContentValue(AtomContentValueType.HTML, content);
						break;
					case "application/xhtml+xml":
						XmlNamespaceManager nsMgr = new XmlNamespaceManager(new NameTable());
						nsMgr.AddNamespace("xhtml", "http://www.w3.org/1999/xhtml");

						if (mode == "xml")
						{
							var div = node.SelectSingleNodeNS("xhtml:div", nsMgr.ToNSMethodFormat());
							if (div != null)
								tv = new AtomContentValue(AtomContentValueType.XHTML, (string)div.NodeValue);
							else
								tv = new AtomContentValue(AtomContentValueType.XHTML, string.Empty);
						}
						else
						{
							tv = new AtomContentValue(AtomContentValueType.XHTML, content);
						}
						break;
					default:
					case "text/plain":
						tv = new AtomContentValue(AtomContentValueType.Text, content);
						break;
				}
				return tv;
			}

			public override XmlElement HtmlToTextNode(XmlDocument ownerDoc, string html)
			{
				XmlElement el = ownerDoc.CreateElementNS(NamespaceUri, "atom:content");
				el.SetAttribute("type", "text/html");
				el.SetAttribute("mode", "escaped");
				el.InnerText = html;
				return el;
			}

			public override XmlElement PlaintextToTextNode(XmlDocument ownerDoc, string text)
			{
				XmlElement el = ownerDoc.CreateElementNS(NamespaceUri, "atom:content");
				el.SetAttribute("type", "text/plain");
				el.SetAttribute("mode", "escaped");
				el.InnerText = text;
				return el;
			}

			public override BlogPostCategory[] ExtractCategories(XmlElement entry, string categoryScheme, Uri documentUri)
			{
				XmlNamespaceManager nsMgr = new XmlNamespaceManager(new NameTable());
				nsMgr.AddNamespace("dc", DC_URI);

				ArrayList results = new ArrayList();

				foreach (XmlElement el in entry.SelectNodesNS("dc:subject", nsMgr.ToNSMethodFormat()))
				{
					string subject = el.InnerText;
					if (subject != string.Empty)
						results.Add(new BlogPostCategory(subject, subject));
				}

				return (BlogPostCategory[]) results.ToArray(typeof(BlogPostCategory));
			}
		}

		private class Atom10ProtocolVersion : AtomProtocolVersion
		{
			public override string NamespaceUri { get { return "http://www.w3.org/2005/Atom"; } }
            public override string AtomPubNamespaceUri { get { return "http://www.w3.org/2007/app"; } }
            public override string PubNamespaceUri { get { return "http://www.w3.org/2007/app"; } }
            public override string UpdatedElName { get { return "updated"; } }
			public override string PublishedElName { get { return "published"; } }

			public override XmlElement CreateCategoryElement(XmlDocument ownerDoc, string category, string categoryScheme, string categoryLabel)
			{
				if (categoryScheme == null)
					throw new ArgumentException("Null category scheme not supported");

				XmlElement element = ownerDoc.CreateElementNS(NamespaceUri, "atom:category");
				element.SetAttribute("term", category);
				if (categoryScheme.Length > 0)
					element.SetAttribute("scheme", categoryScheme);
			    element.SetAttribute("label", categoryLabel);
				return element;
			}
			
			public override void RemoveAllCategories(IXmlNode node, string categoryScheme, Uri documentUri)
			{
				if (categoryScheme == null)
					return;

				XmlNamespaceManager nsMgr = new XmlNamespaceManager(new NameTable());
				nsMgr.AddNamespace("atom", NamespaceUri);
				ArrayList nodesToRemove = new ArrayList();
				foreach (XmlElement categoryEl in node.SelectNodesNS("atom:category", nsMgr.ToNSMethodFormat()))
				{
					string scheme = categoryEl.GetAttribute("scheme");
					if (SchemesEqual(scheme, categoryScheme))
						nodesToRemove.Add(categoryEl);
				}
				foreach (XmlElement categoryEl in nodesToRemove)
					categoryEl.ParentNode.RemoveChild(categoryEl);
			}

			private bool SchemesEqual(string scheme1, string scheme2)
			{
				/*
				if (scheme1 == null)
					scheme1 = "";
				if (scheme2 == null)
					scheme2 = "";
				*/
				return string.Equals(scheme1, scheme2);
			}

			public override string TextNodeToHtml(IXmlNode node)
			{
				return ToTextValue(node).ToHTML();
			}

			public override string TextNodeToPlaintext(IXmlNode node)
			{
				return ToTextValue(node).ToText();
			}

			private static AtomContentValue ToTextValue(IXmlNode target)
			{
				string type = "text";
				var attrType = target.Attributes.SingleOrDefault(a => a.NodeValue == "type");
				if (attrType != null)
					type = (string)attrType.NodeValue;
				switch (type)
				{
					case "html":
						return new AtomContentValue(AtomContentValueType.HTML, target.InnerText.Trim());
					case "xhtml":
					{
						XmlNamespaceManager nsMgr = new XmlNamespaceManager(new NameTable());
						nsMgr.AddNamespace("xhtml", "http://www.w3.org/1999/xhtml");

						var div = target.SelectSingleNodeNS("xhtml:div", nsMgr.ToNSMethodFormat());
						if (div != null)
							return new AtomContentValue(AtomContentValueType.XHTML, (string)div.NodeValue);
						else
							return new AtomContentValue(AtomContentValueType.XHTML, string.Empty);
					}
					default:
					case "text":
						return new AtomContentValue(AtomContentValueType.Text, target.InnerText.Trim());
				}
			}

			public override XmlElement HtmlToTextNode(XmlDocument ownerDoc, string html)
			{
				XmlElement el = ownerDoc.CreateElementNS(NamespaceUri, "atom:content");
				el.SetAttribute("type", "html");
				el.InnerText = html;
				return el;
			}

			public override XmlElement PlaintextToTextNode(XmlDocument ownerDoc, string text)
			{
				XmlElement el = ownerDoc.CreateElementNS(NamespaceUri, "atom:content");
				el.SetAttribute("type", "text");
				el.InnerText = text;
				return el;
			}

			public override BlogPostCategory[] ExtractCategories(XmlElement entry, string categoryScheme, Uri documentUri)
			{
				if (categoryScheme == null)
					return new BlogPostCategory[0];

				XmlNamespaceManager nsMgr = new XmlNamespaceManager(new NameTable());
				nsMgr.AddNamespace("atom", NamespaceUri);

				ArrayList results = new ArrayList();
				foreach (XmlElement el in entry.SelectNodesNS("atom:category", nsMgr.ToNSMethodFormat()))
				{
					if (!SchemesEqual(el.GetAttribute("scheme"), categoryScheme))
						continue;

					string term = el.GetAttribute("term");
					string label = el.GetAttribute("label");

					bool noTerm = term == null || term == string.Empty;
					bool noLabel = label == null || label == string.Empty;

					if (noTerm && noLabel)
						continue;
					if (noTerm)
						term = label;
					if (noLabel)
						label = term;
					results.Add(new BlogPostCategory(term, label));
				}
				return (BlogPostCategory[]) results.ToArray(typeof(BlogPostCategory));
			}
		}

        private class Atom10DraftProtocolVersion : Atom10ProtocolVersion
        {
            public override string AtomPubNamespaceUri { get { return "http://purl.org/atom/app#"; } }
            public override string PubNamespaceUri { get { return "http://purl.org/atom/app#"; } }
        }

        private class Atom10DraftBloggerProtocolVersion : Atom10DraftProtocolVersion
        {
            public override XmlElement CreateCategoryElement(XmlDocument ownerDoc, string category, string categoryScheme, string categoryLabel)
            {
                // Blogger doesn't support category labels and will error out if you pass them
                XmlElement el = base.CreateCategoryElement(ownerDoc, category, categoryScheme, categoryLabel);
                el.RemoveAttribute("label");
                return el;
            }
        }
	}
}
