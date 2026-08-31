import { defineConfig } from 'vitepress'

const localDocsOrigin =
  /^https?:\/\/(?:localhost|127\.0\.0\.1|\[::1\])(?::\d+)?$/

export default defineConfig({
  title: 'WinFormsXaml',
  description: 'High-performance XML interfaces for Windows Forms',
  base: process.env.DOCS_BASE || '/',
  cleanUrls: true,
  lastUpdated: true,
  vite: {
    server: {
      host: '127.0.0.1',
      cors: { origin: localDocsOrigin }
    },
    preview: {
      host: '127.0.0.1',
      cors: { origin: localDocsOrigin }
    },
    plugins: [
      {
        name: 'winforms-xaml-disable-open-in-editor',
        enforce: 'pre',
        configureServer(server) {
          // Vite 5 cannot validate Windows UNC paths safely. The docs do not
          // need editor launching, so terminate this route before Vite sees it.
          server.middlewares.use(
            '/__open-in-editor',
            (_request, response) => {
              response.statusCode = 404
              response.end('Not found')
            }
          )
        }
      }
    ]
  },
  head: [
    ['meta', { name: 'theme-color', content: '#2563eb' }]
  ],
  themeConfig: {
    nav: [
      { text: 'Guide', link: '/guide/getting-started' },
      { text: 'Reference', link: '/reference/runtime' }
    ],
    sidebar: {
      '/guide/': [
        {
          text: 'Start',
          items: [
            { text: 'Getting started', link: '/guide/getting-started' },
            { text: 'Copy-paste templates', link: '/guide/authoring-templates' },
            { text: 'Sample applications', link: '/guide/sample-applications' },
            { text: 'XML IntelliSense', link: '/guide/xml-intellisense' }
          ]
        },
        {
          text: 'Core',
          items: [
            { text: 'Markup and layout', link: '/guide/markup-basics' },
            { text: 'Bindings and functions', link: '/guide/bindings' },
            { text: 'Dynamic presets', link: '/guide/presets' }
          ]
        },
        {
          text: 'Reuse',
          items: [
            { text: 'Reusable components', link: '/guide/components' },
            { text: 'Reusable includes', link: '/guide/includes' }
          ]
        },
        {
          text: 'Controls and layout',
          items: [
            { text: 'Flex layout', link: '/guide/flex-layout' },
            { text: 'TabView', link: '/guide/tab-view' },
            { text: 'ItemsControl', link: '/guide/items-and-virtualization' }
          ]
        },
        {
          text: 'Compatibility',
          items: [
            { text: 'Legacy Windows', link: '/guide/windows-98' }
          ]
        }
      ],
      '/reference/': [
        {
          text: 'Reference',
          items: [
            { text: 'Runtime API', link: '/reference/runtime' },
            { text: 'Markup reference', link: '/reference/markup' },
            { text: 'Performance', link: '/reference/performance' },
            { text: 'Compatibility', link: '/reference/compatibility' },
            { text: 'Validation', link: '/reference/validation' }
          ]
        }
      ]
    },
    search: { provider: 'local' },
    outline: { level: [2, 3] },
    socialLinks: [
      { icon: 'github', link: 'https://github.com/ido-pluto/WinFormsXaml' }
    ]
  }
})
